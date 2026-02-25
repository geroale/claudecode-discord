#!/bin/bash
# Claude Discord Bot - Linux Auto-update & Start Script
# 사용법:
#   ./linux-start.sh          → 백그라운드 실행 (systemd 등록)
#   ./linux-start.sh --fg     → 포그라운드 실행 (디버깅용)
#   ./linux-start.sh --stop   → 중지
#   ./linux-start.sh --status → 상태 확인

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"
SERVICE_NAME="claude-discord"
SERVICE_FILE="$HOME/.config/systemd/user/$SERVICE_NAME.service"

# .env 파일 없으면 초기 설정
if [ ! -f "$ENV_FILE" ]; then
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "  Claude Discord Bot - 초기 설정"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""

    read -p "Discord Bot Token: " BOT_TOKEN
    if [ -z "$BOT_TOKEN" ]; then
        echo "❌ Bot Token은 필수입니다"
        exit 1
    fi

    read -p "Discord Guild(서버) ID: " GUILD_ID
    if [ -z "$GUILD_ID" ]; then
        echo "❌ Guild ID는 필수입니다"
        exit 1
    fi

    read -p "허용할 Discord User ID (여러 명이면 쉼표 구분): " USER_IDS
    if [ -z "$USER_IDS" ]; then
        echo "❌ User ID는 필수입니다"
        exit 1
    fi

    read -p "프로젝트 기본 디렉토리 [$(pwd)]: " PROJECT_DIR
    PROJECT_DIR="${PROJECT_DIR:-$(pwd)}"

    read -p "분당 요청 제한 [10]: " RATE_LIMIT
    RATE_LIMIT="${RATE_LIMIT:-10}"

    echo ""
    echo "Max 플랜 사용자는 비용이 표시되지 않으므로 false 권장"
    read -p "비용 표시 (true/false) [true]: " SHOW_COST
    SHOW_COST="${SHOW_COST:-true}"

    cat > "$ENV_FILE" << EOF
DISCORD_BOT_TOKEN=$BOT_TOKEN
DISCORD_GUILD_ID=$GUILD_ID
ALLOWED_USER_IDS=$USER_IDS
BASE_PROJECT_DIR=$PROJECT_DIR
RATE_LIMIT_PER_MINUTE=$RATE_LIMIT
SHOW_COST=$SHOW_COST
EOF

    echo ""
    echo "✅ .env 파일이 생성되었습니다"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
fi

# node 경로 찾기
find_node() {
    # nvm
    if [ -s "$HOME/.nvm/nvm.sh" ]; then
        . "$HOME/.nvm/nvm.sh"
        which node 2>/dev/null && return
    fi
    # fnm
    if command -v fnm &>/dev/null; then
        eval "$(fnm env)" 2>/dev/null
        which node 2>/dev/null && return
    fi
    # system
    which node 2>/dev/null
}

NODE_BIN=$(find_node)
if [ -z "$NODE_BIN" ]; then
    echo "❌ Node.js를 찾을 수 없습니다"
    exit 1
fi

# --stop: 중지
if [ "$1" = "--stop" ]; then
    systemctl --user stop "$SERVICE_NAME" 2>/dev/null
    echo "🔴 봇 중지됨"
    # 트레이 앱도 종료
    pkill -f "claude_tray.py" 2>/dev/null
    exit 0
fi

# --status: 상태 확인
if [ "$1" = "--status" ]; then
    if systemctl --user is-active "$SERVICE_NAME" &>/dev/null; then
        echo "🟢 봇 실행 중"
        systemctl --user status "$SERVICE_NAME" --no-pager -l 2>/dev/null | head -5
    else
        echo "🔴 봇 중지됨"
    fi
    exit 0
fi

# --fg: 포그라운드 실행
if [ "$1" = "--fg" ]; then
    # nvm 환경 로드
    if [ -s "$HOME/.nvm/nvm.sh" ]; then
        export NVM_DIR="$HOME/.nvm"
        . "$NVM_DIR/nvm.sh"
    fi
    cd "$SCRIPT_DIR"

    echo "[claude-bot] Git 업데이트 확인 중..."
    git fetch origin main 2>/dev/null
    LOCAL=$(git rev-parse HEAD 2>/dev/null)
    REMOTE=$(git rev-parse origin/main 2>/dev/null)

    if [ -n "$LOCAL" ] && [ -n "$REMOTE" ] && [ "$LOCAL" != "$REMOTE" ]; then
        echo "[claude-bot] 업데이트 발견! 자동 업데이트 중..."
        git pull origin main
        npm install --production
        npm run build
        echo "[claude-bot] 업데이트 완료 ($(git log --oneline -1))"
    else
        echo "[claude-bot] 최신 버전"
    fi

    if [ ! -d "dist" ]; then
        echo "[claude-bot] 빌드 파일 없음, 빌드 중..."
        npm run build
    fi

    echo "[claude-bot] 봇 시작 (포그라운드)..."
    exec "$NODE_BIN" dist/index.js
fi

# 기본: 백그라운드 실행 (systemd 등록)

# systemd user 디렉토리 생성
mkdir -p "$HOME/.config/systemd/user"

# 이미 실행 중이면 종료 후 재시작
if systemctl --user is-active "$SERVICE_NAME" &>/dev/null; then
    echo "🔄 기존 봇 종료 중..."
    systemctl --user stop "$SERVICE_NAME"
    sleep 1
fi

# systemd 서비스 파일 생성
cat > "$SERVICE_FILE" << EOF
[Unit]
Description=Claude Discord Bot
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=$SCRIPT_DIR
ExecStart=/bin/bash $SCRIPT_DIR/linux-start.sh --fg
Restart=always
RestartSec=10
StandardOutput=append:$SCRIPT_DIR/bot.log
StandardError=append:$SCRIPT_DIR/bot-error.log

[Install]
WantedBy=default.target
EOF

# 서비스 등록 & 시작
systemctl --user daemon-reload
systemctl --user enable "$SERVICE_NAME" 2>/dev/null
systemctl --user start "$SERVICE_NAME"

# 트레이 앱 실행 (GUI 환경일 때만)
TRAY_SCRIPT="$SCRIPT_DIR/tray/claude_tray.py"
if [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ]; then
    if [ -f "$TRAY_SCRIPT" ] && command -v python3 &>/dev/null; then
        # pystray 설치 확인 및 자동 설치
        if ! python3 -c "import pystray" 2>/dev/null; then
            echo "📦 트레이 앱 의존성 설치 중..."
            pip3 install pystray Pillow 2>/dev/null || pip install pystray Pillow 2>/dev/null
        fi
        if python3 -c "import pystray" 2>/dev/null; then
            pkill -f "claude_tray.py" 2>/dev/null
            nohup python3 "$TRAY_SCRIPT" > /dev/null 2>&1 &
            echo "🟢 봇이 백그라운드에서 시작되었습니다 (트레이 표시)"
        else
            echo "🟢 봇이 백그라운드에서 시작되었습니다"
            echo "   (트레이: pip3 install pystray Pillow 후 재시작)"
        fi
    else
        echo "🟢 봇이 백그라운드에서 시작되었습니다"
    fi
else
    echo "🟢 봇이 백그라운드에서 시작되었습니다 (서버 모드)"
fi
echo "   중지: ./linux-start.sh --stop"
echo "   상태: ./linux-start.sh --status"
echo "   로그: tail -f bot.log"

#!/usr/bin/env python3
"""Claude Discord Bot - Linux System Tray App"""

import subprocess
import os
import sys
import threading
import time

try:
    import pystray
    from PIL import Image, ImageDraw
except ImportError:
    print("필요한 패키지를 설치합니다: pip3 install pystray Pillow")
    subprocess.run([sys.executable, "-m", "pip", "install", "pystray", "Pillow"], check=True)
    import pystray
    from PIL import Image, ImageDraw

SERVICE_NAME = "claude-discord"
BOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ENV_PATH = os.path.join(BOT_DIR, ".env")


def is_running():
    result = subprocess.run(
        ["systemctl", "--user", "is-active", SERVICE_NAME],
        capture_output=True, text=True
    )
    return result.stdout.strip() == "active"


def create_icon(color):
    """Create a colored circle icon"""
    size = 64
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    margin = 8
    draw.ellipse([margin, margin, size - margin, size - margin], fill=color)
    return img


def start_bot(icon, item):
    subprocess.run(["systemctl", "--user", "start", SERVICE_NAME], capture_output=True)
    time.sleep(2)
    update_icon(icon)


def stop_bot(icon, item):
    subprocess.run(["systemctl", "--user", "stop", SERVICE_NAME], capture_output=True)
    time.sleep(1)
    update_icon(icon)


def restart_bot(icon, item):
    subprocess.run(["systemctl", "--user", "restart", SERVICE_NAME], capture_output=True)
    time.sleep(2)
    update_icon(icon)


def open_log(icon, item):
    log_path = os.path.join(BOT_DIR, "bot.log")
    if os.path.exists(log_path):
        subprocess.Popen(["xdg-open", log_path])


def open_folder(icon, item):
    subprocess.Popen(["xdg-open", BOT_DIR])


def edit_settings(icon, item):
    """Open .env file in default editor or zenity dialog"""
    if subprocess.run(["which", "zenity"], capture_output=True).returncode == 0:
        _edit_settings_zenity()
    else:
        # Fallback: open in text editor
        env_path = os.path.join(BOT_DIR, ".env")
        if os.path.exists(env_path):
            subprocess.Popen(["xdg-open", env_path])
        else:
            subprocess.Popen(["xdg-open", os.path.join(BOT_DIR, ".env.example")])


def _edit_settings_zenity():
    """Edit settings using zenity dialogs"""
    env = _load_env()
    fields = [
        ("DISCORD_BOT_TOKEN", "Discord Bot Token"),
        ("DISCORD_GUILD_ID", "Discord Guild ID"),
        ("ALLOWED_USER_IDS", "허용할 User ID (쉼표 구분)"),
        ("BASE_PROJECT_DIR", "프로젝트 기본 디렉토리"),
        ("RATE_LIMIT_PER_MINUTE", "분당 요청 제한"),
        ("SHOW_COST", "비용 표시 (true/false)"),
    ]

    form_args = ["zenity", "--forms", "--title=Claude Discord Bot 설정",
                  "--text=필수 항목을 입력해주세요.\n현재 값을 유지하려면 빈칸으로 두세요."]
    for key, label in fields:
        current = env.get(key, "")
        if key == "DISCORD_BOT_TOKEN" and len(current) > 10:
            display = f"(설정됨: ••••{current[-6:]})"
        elif current:
            display = f"(현재: {current})"
        else:
            display = ""
        form_args.append(f"--add-entry={label} {display}")

    result = subprocess.run(form_args, capture_output=True, text=True)
    if result.returncode != 0:
        return

    values = result.stdout.strip().split("|")
    if len(values) != len(fields):
        return

    new_env = {}
    for i, (key, _) in enumerate(fields):
        val = values[i].strip()
        if val:
            new_env[key] = val
        else:
            new_env[key] = env.get(key, "")

    defaults = {"RATE_LIMIT_PER_MINUTE": "10", "SHOW_COST": "true", "BASE_PROJECT_DIR": BOT_DIR}
    for key, default in defaults.items():
        if not new_env.get(key):
            new_env[key] = default

    if not new_env.get("DISCORD_BOT_TOKEN") or not new_env.get("DISCORD_GUILD_ID") or not new_env.get("ALLOWED_USER_IDS"):
        subprocess.run(["zenity", "--error", "--text=Bot Token, Guild ID, User ID는 필수입니다."])
        return

    with open(ENV_PATH, "w") as f:
        for key, _ in fields:
            if key == "SHOW_COST":
                f.write("# Show estimated API cost in task results (set false for Max plan users)\n")
            f.write(f"{key}={new_env.get(key, '')}\n")


def _load_env():
    env = {}
    if not os.path.exists(ENV_PATH):
        return env
    with open(ENV_PATH) as f:
        for line in f:
            line = line.strip()
            if line.startswith("#") or "=" not in line:
                continue
            key, _, value = line.partition("=")
            env[key.strip()] = value.strip()
    return env


def quit_all(icon, item):
    if is_running():
        subprocess.run(["systemctl", "--user", "stop", SERVICE_NAME], capture_output=True)
    icon.stop()


def update_icon(icon):
    running = is_running()
    color = (76, 175, 80, 255) if running else (244, 67, 54, 255)  # green / red
    icon.icon = create_icon(color)
    icon.title = "Claude Bot: 실행 중" if running else "Claude Bot: 중지됨"


def create_menu():
    running = is_running()
    has_env = os.path.exists(ENV_PATH)

    if not has_env:
        return pystray.Menu(
            pystray.MenuItem("⚙️ 설정이 필요합니다", None, enabled=False),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("초기 설정...", edit_settings),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("종료", quit_all),
        )

    if running:
        return pystray.Menu(
            pystray.MenuItem("🟢 실행 중", None, enabled=False),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("봇 중지", stop_bot),
            pystray.MenuItem("봇 재시작", restart_bot),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("설정 편집...", edit_settings),
            pystray.MenuItem("로그 보기", open_log),
            pystray.MenuItem("폴더 열기", open_folder),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("종료", quit_all),
        )
    else:
        return pystray.Menu(
            pystray.MenuItem("🔴 중지됨", None, enabled=False),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("봇 시작", start_bot),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("설정 편집...", edit_settings),
            pystray.MenuItem("로그 보기", open_log),
            pystray.MenuItem("폴더 열기", open_folder),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("종료", quit_all),
        )


def refresh_loop(icon):
    while icon.visible:
        time.sleep(5)
        try:
            update_icon(icon)
            icon.menu = create_menu()
        except Exception:
            pass


def main():
    running = is_running()
    color = (76, 175, 80, 255) if running else (244, 67, 54, 255)

    icon = pystray.Icon(
        "claude-bot",
        create_icon(color),
        "Claude Bot",
        menu=create_menu(),
    )

    refresh_thread = threading.Thread(target=refresh_loop, args=(icon,), daemon=True)
    refresh_thread.start()

    icon.run()


if __name__ == "__main__":
    main()

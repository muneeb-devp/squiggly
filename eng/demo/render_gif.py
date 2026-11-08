"""Renders the frames from DemoFrames as assets/demo.gif, drawn like VS Code's dark theme.

Every squiggle comes from a real engine mark; this script only draws them. Usage:
    python render_gif.py frames.json ../../assets/demo.gif   (needs Pillow)
"""
import json
import math
import sys

from PIL import Image, ImageDraw, ImageFont

FONT_PATH = "/System/Library/Fonts/Menlo.ttc"
FONT_SIZE = 15
LINE_HEIGHT = 23
GUTTER = 44
PAD_X = 14
TITLE_HEIGHT = 34
PANEL_HEIGHT = 34

BG = (30, 30, 30)
TITLE_BG = (24, 24, 24)
TAB_BG = (30, 30, 30)
GUTTER_FG = (110, 118, 129)
TEXT = (212, 212, 212)
COMMENT = (106, 153, 85)
KEYWORD = (86, 156, 214)
TYPE = (78, 201, 176)
ERROR = (241, 76, 76)
WARNING = (204, 167, 0)
INFO = (170, 170, 170)
CARET = (174, 175, 173)
MENU_BG = (37, 37, 38)
MENU_BORDER = (69, 69, 69)
MENU_SELECTED = (4, 57, 94)
KEYCAP_BG = (60, 60, 60)

SEVERITY = {
    "Primary": "error", "GameOver": "error",
    "Secondary": "warning", "Flag": "warning", "HeaderError": "warning", "CanvasError": "warning",
    "Accent": "info", "Hud": "info", "Covered": "info",
}

font = ImageFont.truetype(FONT_PATH, FONT_SIZE)
small = ImageFont.truetype(FONT_PATH, 12)
big = ImageFont.truetype(FONT_PATH, 20)
char_w = font.getlength("M")


def text_x(column):
    return GUTTER + PAD_X + column * char_w


def line_y(line):
    return TITLE_HEIGHT + 8 + line * LINE_HEIGHT


def draw_code(draw, line_no, text):
    y = line_y(line_no)
    draw.text((GUTTER - 8 - font.getlength(str(line_no + 1)), y + 2), str(line_no + 1), font=font, fill=GUTTER_FG)
    if text.lstrip().startswith("//"):
        draw.text((text_x(0), y + 2), text, font=font, fill=COMMENT)
    elif text.startswith("namespace "):
        draw.text((text_x(0), y + 2), "namespace", font=font, fill=KEYWORD)
        draw.text((text_x(10), y + 2), text[10:-1], font=font, fill=TYPE)
        draw.text((text_x(len(text) - 1), y + 2), ";", font=font, fill=TEXT)
    else:
        draw.text((text_x(0), y + 2), text, font=font, fill=TEXT)


def draw_squiggle(draw, mark):
    kind = SEVERITY[mark["kind"]]
    x0 = text_x(mark["column"])
    y = line_y(mark["line"]) + LINE_HEIGHT - 3
    if kind == "info":
        # VS Code draws these diagnostics as a few dots under the start of the span.
        for i in range(3):
            cx = x0 + 3 + i * 5
            draw.ellipse((cx - 1.5, y - 1.5, cx + 1.5, y + 1.5), fill=INFO)
        return
    color = ERROR if kind == "error" else WARNING
    width = mark["length"] * char_w
    points = [(x0 + t / 2, y + 2.2 * math.sin((x0 + t / 2) * math.pi / 3)) for t in range(0, int(width * 2) + 1)]
    draw.line(points, fill=color, width=2)


def draw_menu(draw, entries):
    x = text_x(0) + 6
    y = line_y(1) + 2
    w = max(font.getlength(e) for e in entries) + 44
    h = len(entries) * LINE_HEIGHT + 8
    draw.rectangle((x, y, x + w, y + h), fill=MENU_BG, outline=MENU_BORDER)
    for i, entry in enumerate(entries):
        ey = y + 4 + i * LINE_HEIGHT
        if i == 0:
            draw.rectangle((x + 2, ey, x + w - 2, ey + LINE_HEIGHT - 1), fill=MENU_SELECTED)
        bulb = (255, 204, 0) if i == 0 else (120, 120, 120)
        draw.ellipse((x + 12, ey + 6, x + 20, ey + 14), fill=bulb)
        draw.text((x + 30, ey + 2), entry, font=font, fill=TEXT)


def draw_keycap(draw, key, canvas_width):
    # Beside the board: the key that was just typed.
    w, h = 44, 40
    x, y = text_x(canvas_width) + 40, line_y(5)
    draw.rounded_rectangle((x, y + 4, x + w, y + h + 4), radius=7, fill=(40, 40, 40))
    draw.rounded_rectangle((x, y, x + w, y + h), radius=7, fill=KEYCAP_BG, outline=(110, 110, 110))
    draw.text((x + w / 2, y + h / 2), key.upper(), font=big, fill=TEXT, anchor="mm")
    draw.text((x + w / 2, y + h + 18), "typed", font=small, fill=GUTTER_FG, anchor="mm")


def render(frame, size, previous_keys):
    width, height = size
    image = Image.new("RGB", size, BG)
    draw = ImageDraw.Draw(image)

    # Title bar with a single tab.
    draw.rectangle((0, 0, width, TITLE_HEIGHT), fill=TITLE_BG)
    for i, color in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
        draw.ellipse((12 + i * 20, 11, 24 + i * 20, 23), fill=color)
    draw.rectangle((86, 6, 196, TITLE_HEIGHT), fill=TAB_BG)
    draw.text((100, 12), "C#  Game.cs", font=small, fill=TEXT)

    for i, text in enumerate(frame["lines"]):
        draw_code(draw, i, text)
    for mark in frame["marks"]:
        draw_squiggle(draw, mark)

    caret = frame["caret"]
    cx = text_x(caret["column"])
    cy = line_y(caret["line"])
    draw.rectangle((cx, cy + 2, cx + 1, cy + LINE_HEIGHT - 2), fill=CARET)

    if frame["menu"]:
        draw_menu(draw, frame["menu"])

    keys_line = next((l for l in frame["lines"] if l.startswith("// keys:")), None)
    keys = keys_line[len("// keys:"):].strip() if keys_line else ""
    if len(keys) > len(previous_keys):
        canvas_width = max(len(l) for l in frame["lines"] if l.startswith("//|"))
        draw_keycap(draw, keys[-1], canvas_width)

    # Problems-panel style line with the header diagnostic: the HUD, or GAME OVER.
    py = height - PANEL_HEIGHT
    draw.rectangle((0, py, width, height), fill=TITLE_BG)
    header_mark = next(m for m in frame["marks"] if m["line"] == 0)
    error = SEVERITY[header_mark["kind"]] == "error"
    icon = ERROR if error else (55, 148, 255)
    draw.ellipse((14, py + 10, 28, py + 24), outline=icon, width=2)
    draw.text((21, py + 17), "x" if error else "i", font=small, fill=icon, anchor="mm")
    message = header_mark["message"]
    max_chars = int((width - 120) / font.getlength("M"))
    if len(message) > max_chars:
        message = message[: max_chars - 1] + "…"
    draw.text((38, py + 9), message, font=font, fill=TEXT)
    draw.text((width - 70, py + 10), "SQ00" + ("11" if error else "10"), font=small, fill=GUTTER_FG)
    return image, keys


def main():
    frames = json.load(open(sys.argv[1]))
    longest = max(len(l) for f in frames for l in f["lines"])
    rows = max(len(f["lines"]) for f in frames)
    size = (int(text_x(longest) + 60), line_y(rows) + 10 + PANEL_HEIGHT)

    images, durations, previous = [], [], ""
    for frame in frames:
        image, previous = render(frame, size, previous)
        images.append(image.quantize(colors=64, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE))
        durations.append(frame["ms"])

    images[0].save(sys.argv[2], save_all=True, append_images=images[1:], duration=durations, loop=0, optimize=True)
    print(f"{len(images)} frames, {size[0]}x{size[1]}, {sum(durations) / 1000:.1f}s")


main()

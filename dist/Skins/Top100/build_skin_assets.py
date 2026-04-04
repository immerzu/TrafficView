from __future__ import annotations

from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = ROOT.parent.parent
SOURCE_IMAGE = PROJECT_ROOT.parent / "Futuristisches Widget mit Geschwindigkeitsanzeige.png"

BASE_WIDTH = 114
BASE_HEIGHT = 56


def scale_dimension(value: int, scale_percent: int) -> int:
    return int(math.floor(((value * scale_percent) / 100.0) + 0.5))


TARGETS = [
    ("TrafficView.panel.90.png", scale_dimension(BASE_WIDTH, 90), scale_dimension(BASE_HEIGHT, 90)),
    ("TrafficView.panel.png", BASE_WIDTH, BASE_HEIGHT),
    ("TrafficView.panel.110.png", scale_dimension(BASE_WIDTH, 110), scale_dimension(BASE_HEIGHT, 110)),
    ("TrafficView.panel.125.png", scale_dimension(BASE_WIDTH, 125), scale_dimension(BASE_HEIGHT, 125)),
    ("TrafficView.panel.150.png", scale_dimension(BASE_WIDTH, 150), scale_dimension(BASE_HEIGHT, 150)),
]


def new_layer(size: tuple[int, int]) -> Image.Image:
    return Image.new("RGBA", size, (0, 0, 0, 0))


def crop_source_widget(source: Image.Image) -> Image.Image:
    width, height = source.size
    pixels = source.load()
    min_x, min_y = width, height
    max_x = max_y = -1

    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a > 0 and not (r > 246 and g > 246 and b > 246):
                min_x = min(min_x, x)
                min_y = min(min_y, y)
                max_x = max(max_x, x)
                max_y = max(max_y, y)

    if max_x < min_x or max_y < min_y:
        return source

    pad_x = max(6, int(round((max_x - min_x + 1) * 0.01)))
    pad_y = max(6, int(round((max_y - min_y + 1) * 0.01)))
    return source.crop(
        (
            max(0, min_x - pad_x),
            max(0, min_y - pad_y),
            min(width, max_x + pad_x + 1),
            min(height, max_y + pad_y + 1),
        )
    )


def clean_text_rows(image: Image.Image) -> Image.Image:
    width, height = image.size

    mask_layer = new_layer(image.size)
    mdraw = ImageDraw.Draw(mask_layer)
    inner_panel = (
        int(round(width * 0.07)),
        int(round(height * 0.11)),
        int(round(width * 0.56)),
        int(round(height * 0.82)),
    )
    mdraw.rounded_rectangle(
        inner_panel,
        radius=max(5, int(round(height * 0.13))),
        fill=(5, 16, 29, 172),
    )

    top_row = (
        int(round(width * 0.09)),
        int(round(height * 0.15)),
        int(round(width * 0.52)),
        int(round(height * 0.37)),
    )
    bottom_row = (
        int(round(width * 0.09)),
        int(round(height * 0.47)),
        int(round(width * 0.52)),
        int(round(height * 0.69)),
    )

    for box in (top_row, bottom_row):
        mdraw.rounded_rectangle(
            box,
            radius=max(4, int(round(height * 0.07))),
            fill=(4, 14, 28, 252),
        )

    image.alpha_composite(mask_layer.filter(ImageFilter.GaussianBlur(max(1, int(round(width * 0.012))))))

    tint_layer = new_layer(image.size)
    tdraw = ImageDraw.Draw(tint_layer)
    for box in (top_row, bottom_row):
        tdraw.rounded_rectangle(
            box,
            radius=max(4, int(round(height * 0.07))),
            fill=(18, 48, 70, 58),
        )

    image.alpha_composite(tint_layer.filter(ImageFilter.GaussianBlur(max(1, int(round(width * 0.008))))))

    sheen_layer = new_layer(image.size)
    sdraw = ImageDraw.Draw(sheen_layer)
    for box in (top_row, bottom_row):
        sdraw.rounded_rectangle(
            box,
            radius=max(4, int(round(height * 0.07))),
            outline=(120, 220, 240, 48),
            width=max(1, int(round(width * 0.01))),
        )

    image.alpha_composite(sheen_layer.filter(ImageFilter.GaussianBlur(1)))
    return image


def reinforce_panel_gloss(image: Image.Image) -> Image.Image:
    width, height = image.size
    layer = new_layer(image.size)
    draw = ImageDraw.Draw(layer)

    draw.rounded_rectangle(
        (4, 2, width - 6, int(round(height * 0.28))),
        radius=max(4, int(round(height * 0.12))),
        fill=(255, 255, 255, 20),
    )
    draw.ellipse(
        (int(round(width * 0.83)), int(round(height * 0.67)), width + 4, height + 4),
        fill=(210, 255, 255, 58),
    )

    image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.02))))))
    return image


def sharpen(image: Image.Image) -> Image.Image:
    return image.filter(ImageFilter.UnsharpMask(radius=1.2, percent=135, threshold=2))


def build_asset(width: int, height: int) -> Image.Image:
    source = Image.open(SOURCE_IMAGE).convert("RGBA")
    source = crop_source_widget(source)
    image = source.resize((width, height), Image.Resampling.LANCZOS)
    image = clean_text_rows(image)
    image = reinforce_panel_gloss(image)
    image = sharpen(image)
    return image


def main() -> None:
    if not SOURCE_IMAGE.exists():
        raise FileNotFoundError(f"Source image not found: {SOURCE_IMAGE}")

    for name, width, height in TARGETS:
        output = build_asset(width, height)
        output.save(ROOT / name)
        print(ROOT / name)


if __name__ == "__main__":
    main()

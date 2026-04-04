from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parent
COMFY_OUTPUT = Path(r"D:\Codex\!Grafikhilfen\ComfyUI\ComfyUI\output\comfy03_widget_rebuild_00001_.png")
FALLBACK_SOURCE = Path(r"D:\Codex\TrafficView_Moi\Futuristisches Widget mit Geschwindigkeitsanzeige.png")

TARGETS = [
    ("TrafficView.panel.90.png", 92, 50),
    ("TrafficView.panel.png", 102, 56),
    ("TrafficView.panel.110.png", 112, 62),
    ("TrafficView.panel.125.png", 128, 70),
    ("TrafficView.panel.150.png", 153, 84),
]


def new_layer(size: tuple[int, int]) -> Image.Image:
    return Image.new("RGBA", size, (0, 0, 0, 0))


def get_source_path() -> Path:
    if COMFY_OUTPUT.exists():
        return COMFY_OUTPUT
    return FALLBACK_SOURCE


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


def clear_text_overlay(image: Image.Image) -> Image.Image:
    width, height = image.size

    mask = new_layer(image.size)
    draw = ImageDraw.Draw(mask)
    left_panel = (
        int(round(width * 0.075)),
        int(round(height * 0.12)),
        int(round(width * 0.56)),
        int(round(height * 0.82)),
    )
    draw.rounded_rectangle(
        left_panel,
        radius=max(4, int(round(height * 0.13))),
        fill=(5, 16, 29, 148),
    )

    rows = [
        (
            int(round(width * 0.09)),
            int(round(height * 0.15)),
            int(round(width * 0.52)),
            int(round(height * 0.37)),
        ),
        (
            int(round(width * 0.09)),
            int(round(height * 0.48)),
            int(round(width * 0.52)),
            int(round(height * 0.69)),
        ),
    ]
    for box in rows:
        draw.rounded_rectangle(
            box,
            radius=max(3, int(round(height * 0.06))),
            fill=(4, 14, 28, 252),
        )

    image.alpha_composite(mask.filter(ImageFilter.GaussianBlur(max(1, int(round(width * 0.012))))))

    edge = new_layer(image.size)
    edraw = ImageDraw.Draw(edge)
    for box in rows:
        edraw.rounded_rectangle(
            box,
            radius=max(3, int(round(height * 0.06))),
            outline=(118, 230, 250, 40),
            width=1,
        )
    image.alpha_composite(edge.filter(ImageFilter.GaussianBlur(1)))
    return image


def clear_center_for_live_overlay(image: Image.Image) -> Image.Image:
    width, height = image.size
    cx = int(round(width * 0.765))
    cy = int(round(height * 0.46))
    outer_r = int(round(height * 0.31))
    mid_r = int(round(height * 0.24))
    inner_r = int(round(height * 0.16))

    clear_layer = new_layer(image.size)
    cdraw = ImageDraw.Draw(clear_layer)
    cdraw.ellipse(
        (cx - outer_r - 8, cy - outer_r - 8, cx + outer_r + 8, cy + outer_r + 8),
        fill=(6, 16, 30, 226),
    )
    image.alpha_composite(clear_layer.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.022))))))

    beam = new_layer(image.size)
    bdraw = ImageDraw.Draw(beam)
    beam_h = max(3, int(round(height * 0.10)))
    bdraw.rounded_rectangle(
        (
            cx - outer_r - int(round(width * 0.16)),
            cy - beam_h // 2,
            min(width - 4, cx + outer_r + int(round(width * 0.12))),
            cy + beam_h // 2,
        ),
        radius=max(2, beam_h // 2),
        fill=(115, 240, 255, 34),
    )
    image.alpha_composite(beam.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.02))))))

    ring = new_layer(image.size)
    rdraw = ImageDraw.Draw(ring)
    rdraw.ellipse(
        (cx - outer_r, cy - outer_r, cx + outer_r, cy + outer_r),
        outline=(38, 190, 255, 114),
        width=max(2, int(round(width * 0.015))),
    )
    rdraw.ellipse(
        (cx - mid_r, cy - mid_r, cx + mid_r, cy + mid_r),
        outline=(56, 128, 255, 90),
        width=max(1, int(round(width * 0.012))),
    )
    rdraw.ellipse(
        (cx - inner_r, cy - inner_r, cx + inner_r, cy + inner_r),
        outline=(102, 240, 255, 94),
        width=max(1, int(round(width * 0.01))),
    )
    image.alpha_composite(ring.filter(ImageFilter.GaussianBlur(max(1, int(round(width * 0.012))))))
    image.alpha_composite(ring)
    return image


def reinforce_gloss(image: Image.Image) -> Image.Image:
    width, height = image.size
    layer = new_layer(image.size)
    draw = ImageDraw.Draw(layer)
    draw.rounded_rectangle(
        (4, 2, width - 5, int(round(height * 0.27))),
        radius=max(3, int(round(height * 0.11))),
        fill=(255, 255, 255, 16),
    )
    draw.ellipse(
        (int(round(width * 0.84)), int(round(height * 0.67)), width + 4, height + 3),
        fill=(210, 255, 255, 52),
    )
    image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.02))))))
    return image


def build_asset(width: int, height: int) -> Image.Image:
    source = Image.open(get_source_path()).convert("RGBA")
    cropped = crop_source_widget(source)
    image = cropped.resize((width, height), Image.Resampling.LANCZOS)
    image = clear_text_overlay(image)
    image = clear_center_for_live_overlay(image)
    image = reinforce_gloss(image)
    image = image.filter(ImageFilter.UnsharpMask(radius=1.1, percent=140, threshold=2))
    return image


def main() -> None:
    source_path = get_source_path()
    if not source_path.exists():
        raise FileNotFoundError(f"Source image not found: {source_path}")

    for filename, width, height in TARGETS:
        output = build_asset(width, height)
        output.save(ROOT / filename)
        print(ROOT / filename)


if __name__ == "__main__":
    main()

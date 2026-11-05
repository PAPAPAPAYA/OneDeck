from io import BytesIO
from pathlib import Path
from urllib.request import urlopen
from PIL import Image, ImageDraw

urls = [
    "https://i.pinimg.com/474x/91/bb/3c/91bb3c2ec260f2c36629b80cb80330e0.jpg",
    "https://i.pinimg.com/474x/06/0f/6e/060f6ec985d5a32140af0612a1987a1e.jpg",
    "https://i.pinimg.com/474x/25/e3/0d/25e30dc95f9434fa0f0e1b39c9deacb0.jpg",
    "https://i.pinimg.com/474x/97/3f/32/973f3209df4463e1f138e69c705107e1.jpg",
    "https://i.pinimg.com/474x/cc/1f/d3/cc1fd34060504830f58bf106568c382b.jpg",
    "https://i.pinimg.com/474x/97/94/c7/9794c760ba55453f1e0b675cc6d93b64.jpg",
    "https://i.pinimg.com/474x/ed/a0/b6/eda0b605fe9adeea6f94175085b6ff12.jpg",
    "https://i.pinimg.com/474x/ff/ed/dc/ffeddc55663bce18b78a5606e8940999.jpg",
    "https://i.pinimg.com/474x/92/cc/f1/92ccf11f895713ef15983c558a6bf152.jpg",
    "https://i.pinimg.com/474x/76/dc/47/76dc471dc013df6590363d36a03fc432.jpg",
    "https://i.pinimg.com/474x/64/b5/98/64b598a32765dddf6bcea618ef9ceb18.jpg",
    "https://i.pinimg.com/474x/93/3a/c5/933ac53aaf42d6ad2c4d0259244fcefb.jpg",
    "https://i.pinimg.com/474x/00/7c/fc/007cfc9fb0f040d5f8b4682c590291b6.jpg",
    "https://i.pinimg.com/474x/eb/83/b1/eb83b1bcfa271148004f3596c79ded20.jpg",
    "https://i.pinimg.com/474x/bf/8e/4c/bf8e4cebe62891d9b4a89890c9358048.jpg",
    "https://i.pinimg.com/474x/00/5f/f6/005ff63dcb2c44deef463c2eacd5d92e.jpg",
    "https://i.pinimg.com/474x/6f/4c/84/6f4c849274a128bbc254905b7ed0bb16.jpg",
    "https://i.pinimg.com/474x/15/e9/1c/15e91cb9df52e18b2c1ca512af84e3b7.jpg",
    "https://i.pinimg.com/474x/aa/97/27/aa972780bfc9161a2801c319bd4d61b3.jpg",
    "https://i.pinimg.com/474x/4d/12/f1/4d12f1ee8310e679c3a0b61fcdcb0afa.jpg",
    "https://i.pinimg.com/474x/f1/67/c4/f167c429906349f3db8459e5eea7c39d.jpg",
    "https://i.pinimg.com/474x/b1/2d/18/b12d18602cf873bd1a2c8e017abca159.jpg",
    "https://i.pinimg.com/474x/0c/f6/24/0cf62431a8ace1b3f65df1b70404582d.jpg",
    "https://i.pinimg.com/474x/11/47/ab/1147ab7ad65e239d08973210e2bbff5a.jpg",
]
cell_w, cell_h = 220, 260
cols = 6
rows = (len(urls) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cell_w, rows * cell_h), "white")
draw = ImageDraw.Draw(sheet)
for i, url in enumerate(urls):
    try:
        with urlopen(url, timeout=20) as response:
            image = Image.open(BytesIO(response.read())).convert("RGB")
        image.thumbnail((cell_w - 12, cell_h - 28))
        x = (i % cols) * cell_w + (cell_w - image.width) // 2
        y = (i // cols) * cell_h + 20
        sheet.paste(image, (x, y))
        draw.text(((i % cols) * cell_w + 8, (i // cols) * cell_h + 3), str(i + 1), fill="black")
    except Exception as exc:
        draw.text(((i % cols) * cell_w + 8, (i // cols) * cell_h + 30), f"{i + 1}: {exc}", fill="red")
out = Path(__file__).with_name("pinterest-contact-sheet.jpg")
sheet.save(out, quality=90)
print(out)

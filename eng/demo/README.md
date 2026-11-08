# Demo GIF

`assets/demo.gif` is generated, not screen-recorded, so it always matches the current engine.

1. `DemoFrames` plays a scripted Snake game through `GameFile`, the same code the analyzer runs, and writes every frame (file lines, diagnostic marks, caret, menu) to JSON.
2. `render_gif.py` draws those frames in VS Code's dark-theme colours and encodes the GIF. It needs Python 3 with Pillow, and the Menlo font (macOS).

Regenerate it after changing game rules, messages or layout:

```sh
python3 -m venv /tmp/squiggly-demo && /tmp/squiggly-demo/bin/pip install pillow
dotnet run --project eng/demo/DemoFrames -- /tmp/frames.json
/tmp/squiggly-demo/bin/python eng/demo/render_gif.py /tmp/frames.json assets/demo.gif
```

The script draws squiggles where the analyzer reports them; colours are VS Code's (red errors, yellow warnings, grey dots for info). A real IDE recording for the launch post is still worth making.

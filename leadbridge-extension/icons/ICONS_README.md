# Extension Icons

You'll need to create three icon sizes:

- `icon16.png` - 16x16px (toolbar)
- `icon48.png` - 48x48px (extension management)
- `icon128.png` - 128x128px (Chrome Web Store)

## Quick Icon Creation

Use any of these methods:

1. **Online Tool**: Use https://www.favicon-generator.org/
2. **Figma/Canva**: Design and export at required sizes
3. **ImageMagick** (if installed):
   ```bash
   convert -size 128x128 xc:none -fill "#667eea" -draw "circle 64,64 64,10" icon128.png
   convert icon128.png -resize 48x48 icon48.png
   convert icon128.png -resize 16x16 icon16.png
   ```

## Design Suggestion

- Use a bridge icon (🧱 brick or 🌉 bridge)
- Colors: Purple gradient (#667eea to #764ba2) matching popup
- Include "LB" or "LeadBridge" text

## Temporary Placeholder

For development, you can use any PNG images temporarily. The extension will still work without icons, but Chrome will show a warning.

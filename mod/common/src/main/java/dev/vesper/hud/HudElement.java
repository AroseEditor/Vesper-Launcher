package dev.vesper.hud;

public final class HudElement {

    public int x;
    public int y;
    public float scale;
    public boolean enabled;
    public int colour = 0xFFB57EDC;
    public boolean shadow = true;

    public HudElement(int x, int y, float scale, boolean enabled) {
        this.x = x;
        this.y = y;
        this.scale = scale;
        this.enabled = enabled;
    }

    public int width(int textWidth) {
        return Math.round(textWidth * scale);
    }

    public int height(int lineHeight) {
        return Math.round(lineHeight * scale);
    }

    public boolean contains(int pointX, int pointY, int textWidth, int lineHeight) {
        return pointX >= x
                && pointY >= y
                && pointX <= x + width(textWidth)
                && pointY <= y + height(lineHeight);
    }

    public void moveTo(int newX, int newY, int screenWidth, int screenHeight, int textWidth, int lineHeight) {
        int maxX = Math.max(0, screenWidth - width(textWidth));
        int maxY = Math.max(0, screenHeight - height(lineHeight));

        x = Math.max(0, Math.min(newX, maxX));
        y = Math.max(0, Math.min(newY, maxY));
    }

    public void snap(int gridSize) {
        if (gridSize <= 1) {
            return;
        }

        x = Math.round((float) x / gridSize) * gridSize;
        y = Math.round((float) y / gridSize) * gridSize;
    }
}

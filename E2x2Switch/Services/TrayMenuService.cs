using System.Drawing.Drawing2D;
using GdiColor = System.Drawing.Color;
using GdiPen = System.Drawing.Pen;
using GdiRectangle = System.Drawing.Rectangle;

namespace E2x2Switch.Services;

/// <summary>Renders system tray context menus matching Windows Dark and Light themes with Fluent hover states.</summary>
internal sealed class ThemeAwareContextMenuRenderer(bool isLight) : ToolStripProfessionalRenderer(new ThemeAwareColorTable(isLight))
{
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected)
        {
            base.OnRenderMenuItemBackground(e);
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new GdiRectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
        var hoverColor = isLight ? GdiColor.FromArgb(232, 232, 232) : GdiColor.FromArgb(58, 60, 65);

        using var brush = new SolidBrush(hoverColor);
        using var path = CreateRoundedRectangle(bounds, 4);
        g.FillPath(brush, path);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var bounds = new GdiRectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        var borderColor = isLight ? GdiColor.FromArgb(220, 220, 220) : GdiColor.FromArgb(56, 58, 62);

        using var pen = new GdiPen(borderColor, 1);
        e.Graphics.DrawRectangle(pen, bounds);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        var sepColor = isLight ? GdiColor.FromArgb(225, 225, 225) : GdiColor.FromArgb(54, 56, 60);

        using var pen = new GdiPen(sepColor, 1);
        e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
    }

    private static GraphicsPath CreateRoundedRectangle(GdiRectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        var arc = new GdiRectangle(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}

/// <summary>Color table mapping for Windows Dark and Light context menus.</summary>
internal sealed class ThemeAwareColorTable(bool isLight) : ProfessionalColorTable
{
    public override GdiColor ToolStripDropDownBackground => isLight ? GdiColor.FromArgb(249, 249, 249) : GdiColor.FromArgb(43, 43, 43);

    public override GdiColor MenuBorder => isLight ? GdiColor.FromArgb(220, 220, 220) : GdiColor.FromArgb(60, 60, 60);

    public override GdiColor MenuItemBorder => GdiColor.Transparent;

    public override GdiColor MenuItemSelected => isLight ? GdiColor.FromArgb(230, 230, 230) : GdiColor.FromArgb(58, 60, 65);

    public override GdiColor MenuItemSelectedGradientBegin => MenuItemSelected;
    public override GdiColor MenuItemSelectedGradientEnd => MenuItemSelected;
    public override GdiColor ImageMarginGradientBegin => ToolStripDropDownBackground;
    public override GdiColor ImageMarginGradientMiddle => ToolStripDropDownBackground;
    public override GdiColor ImageMarginGradientEnd => ToolStripDropDownBackground;

    public override GdiColor SeparatorDark => isLight ? GdiColor.FromArgb(225, 225, 225) : GdiColor.FromArgb(60, 60, 60);

    public override GdiColor SeparatorLight => GdiColor.Transparent;
}

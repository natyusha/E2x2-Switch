using System.Drawing.Drawing2D;
using GdiColor = System.Drawing.Color;
using GdiPen = System.Drawing.Pen;
using GdiRectangle = System.Drawing.Rectangle;

namespace E2x2Switch.Services;

#region Renderer

/// <summary>Renders system tray context menus matching Windows Dark and Light themes with Fluent hover states.</summary>
/// <param name="isLight">Whether the active taskbar theme is light.</param>
internal sealed class ThemeAwareContextMenuRenderer(bool isLight) : ToolStripProfessionalRenderer(new ThemeAwareColorTable(isLight))
{
    /// <summary>Draws rounded Fluent hover highlights behind selected menu items.</summary>
    /// <param name="eventArgs">Rendering event data for the menu item.</param>
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs eventArgs)
    {
        if (!eventArgs.Item.Selected)
        {
            base.OnRenderMenuItemBackground(eventArgs);
            return;
        }

        Graphics graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new GdiRectangle(2, 1, eventArgs.Item.Width - 4, eventArgs.Item.Height - 2);
        GdiColor hoverColor = isLight ? GdiColor.FromArgb(232, 232, 232) : GdiColor.FromArgb(58, 60, 65);

        using var brush = new SolidBrush(hoverColor);
        using GraphicsPath path = CreateRoundedRectangle(bounds, 4);
        graphics.FillPath(brush, path);
    }

    /// <summary>Draws a clean 1px border around the context menu strip.</summary>
    /// <param name="eventArgs">Rendering event data for the toolstrip border.</param>
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs eventArgs)
    {
        var bounds = new GdiRectangle(0, 0, eventArgs.ToolStrip.Width - 1, eventArgs.ToolStrip.Height - 1);
        GdiColor borderColor = isLight ? GdiColor.FromArgb(220, 220, 220) : GdiColor.FromArgb(56, 58, 62);

        using var pen = new GdiPen(borderColor, 1);
        eventArgs.Graphics.DrawRectangle(pen, bounds);
    }

    /// <summary>Draws a subtle horizontal separator line across the menu item.</summary>
    /// <param name="eventArgs">Rendering event data for the separator.</param>
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs eventArgs)
    {
        int centerY = eventArgs.Item.Height / 2;
        GdiColor separatorColor = isLight ? GdiColor.FromArgb(225, 225, 225) : GdiColor.FromArgb(54, 56, 60);

        using var pen = new GdiPen(separatorColor, 1);
        eventArgs.Graphics.DrawLine(pen, 4, centerY, eventArgs.Item.Width - 4, centerY);
    }

    private static GraphicsPath CreateRoundedRectangle(GdiRectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        var arcRectangle = new GdiRectangle(rectangle.X, rectangle.Y, diameter, diameter);

        path.AddArc(arcRectangle, 180, 90);
        arcRectangle.X = rectangle.Right - diameter;
        path.AddArc(arcRectangle, 270, 90);
        arcRectangle.Y = rectangle.Bottom - diameter;
        path.AddArc(arcRectangle, 0, 90);
        arcRectangle.X = rectangle.Left;
        path.AddArc(arcRectangle, 90, 90);
        path.CloseFigure();

        return path;
    }
}

#endregion

#region Color Table

/// <summary>Color table mapping for Windows Dark and Light context menus.</summary>
/// <param name="isLight">Whether the active taskbar theme is light.</param>
internal sealed class ThemeAwareColorTable(bool isLight) : ProfessionalColorTable
{
    /// <summary>Gets the dropdown background color.</summary>
    public override GdiColor ToolStripDropDownBackground => isLight ? GdiColor.FromArgb(249, 249, 249) : GdiColor.FromArgb(43, 43, 43);

    /// <summary>Gets the outer menu border color.</summary>
    public override GdiColor MenuBorder => isLight ? GdiColor.FromArgb(220, 220, 220) : GdiColor.FromArgb(60, 60, 60);

    /// <summary>Gets the hovered item border color.</summary>
    public override GdiColor MenuItemBorder => GdiColor.Transparent;

    /// <summary>Gets the hovered item background color.</summary>
    public override GdiColor MenuItemSelected => isLight ? GdiColor.FromArgb(230, 230, 230) : GdiColor.FromArgb(58, 60, 65);

    /// <summary>Gets the hovered item gradient start color.</summary>
    public override GdiColor MenuItemSelectedGradientBegin => MenuItemSelected;

    /// <summary>Gets the hovered item gradient end color.</summary>
    public override GdiColor MenuItemSelectedGradientEnd => MenuItemSelected;

    /// <summary>Gets the image margin gradient start color.</summary>
    public override GdiColor ImageMarginGradientBegin => ToolStripDropDownBackground;

    /// <summary>Gets the image margin gradient middle color.</summary>
    public override GdiColor ImageMarginGradientMiddle => ToolStripDropDownBackground;

    /// <summary>Gets the image margin gradient end color.</summary>
    public override GdiColor ImageMarginGradientEnd => ToolStripDropDownBackground;

    /// <summary>Gets the separator line color.</summary>
    public override GdiColor SeparatorDark => isLight ? GdiColor.FromArgb(225, 225, 225) : GdiColor.FromArgb(60, 60, 60);

    /// <summary>Gets the secondary separator highlight color.</summary>
    public override GdiColor SeparatorLight => GdiColor.Transparent;
}

#endregion

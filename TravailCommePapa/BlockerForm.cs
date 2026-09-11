using System.Drawing.Drawing2D;

namespace TravailCommePapa;

/// <summary>Couvre un écran secondaire pour que rien d'autre ne soit visible ni cliquable.</summary>
internal sealed class BlockerForm : Form
{
    private readonly Form _owner;

    public BlockerForm(Form owner, Screen screen)
    {
        _owner = owner;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(200, 225, 255);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(ClientRectangle,
            Color.FromArgb(190, 225, 255), Color.FromArgb(255, 210, 235), LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        BeginInvoke(() => _owner.Activate());
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Seule la fenêtre principale peut fermer ces écrans
        if (e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }
}

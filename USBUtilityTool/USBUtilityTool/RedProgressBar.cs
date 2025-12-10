using System.Windows.Forms;
using System.Drawing;

// Esta clase obliga a la barra a usar el modo "Bloque" que permite colores en sistemas antiguos,
// o intenta usar estilos visuales en sistemas modernos.
public class RedProgressBar : ProgressBar
{
    public RedProgressBar()
    {
        // Este modo es clave para que el color se respete en muchos sistemas
        this.SetStyle(ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Usamos System.Drawing para el color rojo (o el que quieras)
        SolidBrush brush = new SolidBrush(System.Drawing.Color.Red);

        // Dibujamos el fondo
        e.Graphics.FillRectangle(new SolidBrush(System.Drawing.Color.White), 0, 0, this.Width, this.Height);

        // Calculamos el ancho de la barra llenada
        Rectangle rect = new Rectangle(0, 0, (int)(this.Width * ((double)this.Value / this.Maximum)), this.Height);

        // Dibujamos la barra roja
        e.Graphics.FillRectangle(brush, rect);

        brush.Dispose();
    }
}

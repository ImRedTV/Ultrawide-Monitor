using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowProbe;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		ApplicationConfiguration.Initialize();
		using Form form = new Form
		{
			Text = "Ultrawide Toys Window Probe",
			Width = 720,
			Height = 480,
			StartPosition = FormStartPosition.CenterScreen,
			MinimumSize = new Size(320, 240)
		};
		form.Controls.Add(new Label
		{
			Text = "Maximisez cette fenêtre pour tester la zone active.",
			AutoSize = true,
			Location = new Point(24, 24)
		});
		Application.Run(form);
	}
}


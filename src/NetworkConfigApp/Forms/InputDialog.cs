using System.Drawing;
using System.Windows.Forms;

namespace NetworkConfigApp.Forms
{
    /// <summary>
    /// Simple input dialog for getting text from the user.
    /// </summary>
    public class InputDialog : Form
    {
        private TextBox txtInput;
        private Button btnOk;
        private Button btnCancel;

        public string InputText => txtInput.Text;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Text = title;
            Size = new Size(400, 150);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblPrompt = new Label
            {
                Text = prompt,
                Location = new Point(10, 15),
                AutoSize = true
            };

            txtInput = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(365, 23),
                Text = defaultValue
            };

            btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(210, 75),
                Size = new Size(80, 28)
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(295, 75),
                Size = new Size(80, 28)
            };

            Controls.AddRange(new Control[] { lblPrompt, txtInput, btnOk, btnCancel });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}

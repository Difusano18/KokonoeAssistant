using System.Windows;
using System.Windows.Input;

namespace KokonoeAssistant
{
    public partial class TgAuthDialog : Window
    {
        public string Answer { get; private set; } = "";

        public TgAuthDialog(string prompt)
        {
            InitializeComponent();
            PromptText.Text = prompt;
            InputBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Answer = InputBox.Text.Trim();
            DialogResult = true;
        }

        private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) Ok_Click(sender, e);
        }
    }
}

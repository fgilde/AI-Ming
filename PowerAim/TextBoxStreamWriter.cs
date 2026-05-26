using System.IO;
using System.Text;
using System.Windows.Controls;

namespace PowerAim;

public class TextBoxStreamWriter(TextBox output) : TextWriter
{
    public override void Write(char value)
    {
        //base.Write(value);
        Write(value.ToString());
    }

    public override void Write(string value)
    {
       // base.Write(value);
        output.Dispatcher.BeginInvoke(new Action(() =>
        {
            // Add text to the beginning of the TextBox
            output.Text = value + output.Text;
        }));
    }

    public override Encoding Encoding => Encoding.UTF8;
}
using System.ComponentModel;
using PowerAim.AILogic;
using PowerAim.Types;

namespace PowerAim.Models;

public class RelativeRectModel : INotifyPropertyChanged
{
    public RelativeRectModel(RelativeRect rect): this(rect.WidthPercentage, rect.HeightPercentage, rect.LeftMarginPercentage, rect.TopMarginPercentage)
    {}

    public RelativeRectModel(float widthPercentage, float heightPercentage, float leftMarginPercentage, float topMarginPercentage)
    {
        WidthPercentage = widthPercentage;
        HeightPercentage = heightPercentage;
        LeftMarginPercentage = leftMarginPercentage;
        TopMarginPercentage = topMarginPercentage;
    }

    public float WidthPercentage
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(WidthPercentage));
        }
    }

    public float HeightPercentage
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(HeightPercentage));
        }
    }

    public float LeftMarginPercentage
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(LeftMarginPercentage));
        }
    }

    public float TopMarginPercentage
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(TopMarginPercentage));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    internal RelativeRect ToRelativeRect()
    {
        return new RelativeRect(WidthPercentage, HeightPercentage, LeftMarginPercentage, TopMarginPercentage);
    }

    public override string ToString()
    {
        return ToRelativeRect().ToString();
    }

    protected void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

using Aimmy2.InputLogic;
using Nextended.Core.Types;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Visuality;

namespace Aimmy2.Config;

public abstract class BaseSettings : BaseSettings<object>
{

    //public Dictionary<string, object> PropertyValues { get; set; }

    //// TODO: Remove reflection indexer
    //public object? this[string propertyName]
    //{
    //    get
    //    {
    //        PropertyValues ??= new();
    //        var name = PrepareName(propertyName);
    //        PropertyInfo? property = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
    //        return property == null ? PropertyValues.GetValueOrDefault(propertyName) : property.GetValue(this);
    //    }
    //    set
    //    {
    //        PropertyValues ??= new();
    //        var name = PrepareName(propertyName);
    //        PropertyInfo? property = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
    //        if (property == null)
    //            PropertyValues[propertyName] = value;
    //        else
    //            property.SetValue(this, value);
    //    }
    //}

    // TODO: Remove and just store hashed values for minimized boxes
    protected string PrepareName(string name)
    {
        name = name.Replace("(Up/Down)", "").Replace("(Left/Right)", "");
        var res = name.Replace(" ", "").Replace(":", "").Replace("(", "").Replace(")", "").Replace("/", "").Replace("\\", "").Replace("?", "").Replace("!", "").Replace("'", "").Replace("\"", "").Replace(";", "").Replace(",", "").Replace(".", "").Replace("[", "").Replace("]", "").Replace("{", "").Replace("}", "").Replace("|", "").Replace("=", "").Replace("+", "").Replace("-", "").Replace("*", "").Replace("&", "").Replace("^", "").Replace("%", "").Replace("$", "").Replace("#", "").Replace("@", "").Replace("~", "").Replace("`", "").Replace("<", "").Replace(">", "").Replace(" ", "");
        return res;
    }
}

public abstract class BaseSettings<T> : INotifyPropertyChanged
{
    public Dictionary<string, T>? DynamicPropertyValues { get; set; }

    public T this[string propertyName]
    {
        get => Get(default, propertyName);
        set => Set(value, propertyName);
    }

    protected virtual T Get(T? defaultValue = default, [CallerMemberName] string propertyName = "") =>
        DynamicPropertyValues == null || !DynamicPropertyValues.ContainsKey(propertyName)
            ? defaultValue ?? default
            : DynamicPropertyValues[propertyName];

    protected virtual void Set(T value, [CallerMemberName] string propertyName = "")
    {
        DynamicPropertyValues ??= new();
        DynamicPropertyValues[propertyName] = value;
        OnPropertyChanged(propertyName);
    }

    protected void RaiseAllPropertiesChanged()
    {
        var processedObjects = new HashSet<object>();
        RaiseAllPropertiesChanged(processedObjects);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void RaiseAllPropertiesChanged(HashSet<object> processedObjects)
    {
        // Mark this object as processed
        if (!processedObjects.Add(this))
        {
            // If the object was already processed, return to avoid infinite recursion
            return;
        }

        GetType().GetProperties().ToList().ForEach(p =>
        {
            if (typeof(BaseSettings).IsAssignableFrom(p.PropertyType))
            {
                var settingsObj = p.GetValue(this) as BaseSettings;
                settingsObj?.RaiseAllPropertiesChanged(processedObjects);
            }
            else
            {
                OnPropertyChanged(p.Name);
            }
        });
    }

    public void Load<TSettings>(string path) where TSettings : BaseSettings
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var obj = JsonSerializer.Deserialize<TSettings>(json);
                foreach (var property in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var value = property.GetValue(obj);
                    property.SetValue(this, value);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            new NoticeBar($"{ex.Message}", 5000).Show();
        }

    }

    public void Save<TSettings>(string path) where TSettings : BaseSettings
    {
        try
        {
            string json = JsonSerializer.Serialize(this as TSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }
}
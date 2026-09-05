using System.ComponentModel;

namespace LadderToArduino.Models
{
    public class SimBoolItem : INotifyPropertyChanged
    {
        public string Label { get; set; }
        private bool _value;
        public bool Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class SimIntItem : INotifyPropertyChanged
    {
        // Address prefix (e.g. "AI0") kept separate from the display Label so the label
        // can be refreshed each tick to show "AI0: 512" without losing the raw address.
        public string Address { get; set; }
        private string _label;
        public string Label { get => _label; set { _label = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label))); } }
        private int _value;
        public int Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class XRefRow
    {
        public string Address { get; set; }
        public string UsedAsContactIn { get; set; }
        public string UsedAsCoilIn { get; set; }
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;

namespace LadderToArduino.Models
{
    // One contact placed in series inside a branch. e.g.  I0 --| |--
    // If Kind == AnalogInput, Operator/Threshold are used instead of Mode to build a comparator.
    public class Contact : INotifyPropertyChanged
    {
        private AddressKind _kind = AddressKind.Input;
        private int _index = 0;
        private ContactMode _mode = ContactMode.NormallyOpen;
        private ComparatorOp _operator = ComparatorOp.GreaterThan;
        private int _threshold = 512;

        public AddressKind Kind { get => _kind; set { _kind = value; OnChanged(nameof(Kind)); OnChanged(nameof(Label)); } }
        public int Index { get => _index; set { _index = value; OnChanged(nameof(Index)); OnChanged(nameof(Label)); } }
        public ContactMode Mode { get => _mode; set { _mode = value; OnChanged(nameof(Mode)); OnChanged(nameof(Label)); } }
        public ComparatorOp Operator { get => _operator; set { _operator = value; OnChanged(nameof(Operator)); OnChanged(nameof(Label)); } }
        public int Threshold { get => _threshold; set { _threshold = value; OnChanged(nameof(Threshold)); OnChanged(nameof(Label)); } }

        public string Address => AddressText.From(Kind, Index);

        public string Label => Kind == AddressKind.AnalogInput
            ? $"{Address} {OpSymbol()} {Threshold}"
            : (Mode == ContactMode.NormallyClosed ? "/" : "") + Address;

        private string OpSymbol()
        {
            switch (Operator)
            {
                case ComparatorOp.GreaterThan: return ">";
                case ComparatorOp.GreaterOrEqual: return ">=";
                case ComparatorOp.LessThan: return "<";
                case ComparatorOp.LessOrEqual: return "<=";
                case ComparatorOp.Equal: return "==";
                default: return "?";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // A single parallel path in a rung. Contacts inside a branch are AND-ed together.
    public class Branch
    {
        public ObservableCollection<Contact> Contacts { get; set; } = new ObservableCollection<Contact>();
    }

    // One output element driven by a rung's result. A rung can have several of these (multi-output rungs).
    public class CoilOutput : INotifyPropertyChanged
    {
        private CoilType _coilType = CoilType.Output;
        private AddressKind _outputKind = AddressKind.Output;
        private int _outputIndex = 0;
        private int _preset = 1000; // ms for timers, count for counters

        // Optional external reset for Timer/Counter coils. ResetIndex < 0 means "no reset wired".
        private AddressKind _resetKind = AddressKind.Memory;
        private int _resetIndex = -1;

        // Only used when CoilType == AnalogOutput
        private AnalogSource _analogSource = AnalogSource.Constant;
        private int _analogConstant = 128;
        private int _analogSourceIndex = 0;

        public CoilType CoilType { get => _coilType; set { _coilType = value; OnChanged(nameof(CoilType)); OnChanged(nameof(Label)); } }
        public AddressKind OutputKind { get => _outputKind; set { _outputKind = value; OnChanged(nameof(OutputKind)); OnChanged(nameof(Label)); } }
        public int OutputIndex { get => _outputIndex; set { _outputIndex = value; OnChanged(nameof(OutputIndex)); OnChanged(nameof(Label)); } }
        public int Preset { get => _preset; set { _preset = value; OnChanged(nameof(Preset)); } }

        public AddressKind ResetKind { get => _resetKind; set { _resetKind = value; OnChanged(nameof(ResetKind)); } }
        public int ResetIndex { get => _resetIndex; set { _resetIndex = value; OnChanged(nameof(ResetIndex)); } }

        public AnalogSource AnalogSource { get => _analogSource; set { _analogSource = value; OnChanged(nameof(AnalogSource)); } }
        public int AnalogConstant { get => _analogConstant; set { _analogConstant = value; OnChanged(nameof(AnalogConstant)); } }
        public int AnalogSourceIndex { get => _analogSourceIndex; set { _analogSourceIndex = value; OnChanged(nameof(AnalogSourceIndex)); } }

        public string OutputAddress => AddressText.From(OutputKind, OutputIndex);
        public string Label => $"{CoilSymbol()} {OutputAddress}";

        private string CoilSymbol()
        {
            switch (CoilType)
            {
                case CoilType.Output: return "( )";
                case CoilType.Set: return "(S)";
                case CoilType.Reset: return "(R)";
                case CoilType.TimerOnDelay: return "(TON)";
                case CoilType.TimerOffDelay: return "(TOF)";
                case CoilType.CounterUp: return "(CTU)";
                case CoilType.CounterDown: return "(CTD)";
                case CoilType.AnalogOutput: return "(PWM)";
                default: return "( )";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // One rung of the ladder. Branches are OR-ed together, feeding one or more output elements.
    public class Rung : INotifyPropertyChanged
    {
        private string _comment = "";

        public ObservableCollection<Branch> Branches { get; set; } = new ObservableCollection<Branch>();
        public ObservableCollection<CoilOutput> Outputs { get; set; } = new ObservableCollection<CoilOutput>();

        public string Comment { get => _comment; set { _comment = value; OnChanged(nameof(Comment)); } }

        public Rung()
        {
            var b = new Branch();
            b.Contacts.Add(new Contact());
            Branches.Add(b);
            Outputs.Add(new CoilOutput());
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Maps a logical address to a physical Arduino pin number
    public class PinMapping : INotifyPropertyChanged
    {
        private AddressKind _kind;
        private int _index;
        private int _pin;
        private bool _pullup;

        public AddressKind Kind { get => _kind; set { _kind = value; OnChanged(nameof(Kind)); } }
        public int Index { get => _index; set { _index = value; OnChanged(nameof(Index)); } }
        public int Pin { get => _pin; set { _pin = value; OnChanged(nameof(Pin)); } }
        public bool UseInternalPullup { get => _pullup; set { _pullup = value; OnChanged(nameof(UseInternalPullup)); } }

        public string Address => AddressText.From(Kind, Index);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LadderProgram
    {
        public ObservableCollection<Rung> Rungs { get; set; } = new ObservableCollection<Rung>();
        public ObservableCollection<PinMapping> PinMap { get; set; } = new ObservableCollection<PinMapping>();
        public string BoardFqbn { get; set; } = "arduino:avr:uno";
        public int ScanDelayMs { get; set; } = 10;
    }

    public static class AddressText
    {
        public static string From(AddressKind kind, int index)
        {
            string prefix;
            switch (kind)
            {
                case AddressKind.Input: prefix = "I"; break;
                case AddressKind.Output: prefix = "Q"; break;
                case AddressKind.Memory: prefix = "M"; break;
                case AddressKind.Timer: prefix = "T"; break;
                case AddressKind.Counter: prefix = "C"; break;
                case AddressKind.AnalogInput: prefix = "AI"; break;
                case AddressKind.PWMOutput: prefix = "AO"; break;
                default: prefix = "?"; break;
            }
            return $"{prefix}{index}";
        }
    }
}

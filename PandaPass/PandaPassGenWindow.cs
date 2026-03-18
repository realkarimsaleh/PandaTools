using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public class PandaPassGenWindow : Form
{
    //######################################
    //Singleton Instance Tracker
    //######################################
    private static PandaPassGenWindow? _instance;

    public static void ShowWindow()
    {
        if (_instance == null || _instance.IsDisposed)
        {
            _instance = new PandaPassGenWindow();
            _instance.Show();
        }
        else
        {
            if (_instance.WindowState == FormWindowState.Minimized)
                _instance.WindowState = FormWindowState.Normal;

            _instance.BringToFront();
            _instance.Activate();
        }
    }

    private TextBox txtPassword = null!;
    private NumericUpDown numLength = null!, numUpper = null!, numNumbers = null!, numSymbols = null!;
    private CheckBox chkSpeakEasy = null!, chkPhrases = null!;
    private Button btnGenerate = null!, btnCopy = null!, btnPhonetic = null!;
    
    //Path to the local dictionary cache
    private readonly string _dictPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pandapass_words.txt");

    private PandaPassGenWindow()
    {
        Text = "Panda Password Generator";
        Size = new Size(460, 420);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Icon = AppIcon.Get();

        BuildLayout();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        
        btnGenerate.Enabled = false;
        btnGenerate.Text = "⏳ Fetching Vocabulary...";
        
        await EnsureDictionaryCachedAsync();
        
        btnGenerate.Text = "Generate Password";
        btnGenerate.Enabled = true;
        
        GeneratePassword(); 
    }

    //######################################
    //Save settings automatically when closing
    //######################################
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        var cfg = ConfigLoader.AppConfig;
        
        cfg.PandaPassGen.DefaultLength = (int)numLength.Value;
        cfg.PandaPassGen.DefaultUpper = (int)numUpper.Value;
        cfg.PandaPassGen.DefaultNumbers = (int)numNumbers.Value;
        cfg.PandaPassGen.DefaultSymbols = (int)numSymbols.Value;
        cfg.PandaPassGen.DefaultSpeakEasy = chkSpeakEasy.Checked;
        
        ConfigLoader.Save(cfg);
        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        var defaults = ConfigLoader.AppConfig.PandaPassGen;

        //Header
        var header = new Panel { Left = 0, Top = 0, Width = ClientSize.Width, Height = 60, BackColor = Color.FromArgb(28, 28, 28) };
        header.Controls.Add(new Label { Text = "🐼 Panda Password Generator", Left = 20, Top = 15, Width = 300, Height = 30, ForeColor = Color.White, Font = new Font("Segoe UI", 14f, FontStyle.Bold) });
        Controls.Add(header);

        //Password Display Box
        txtPassword = new TextBox
        {
            Left = 20, Top = 80, Width = 270, Height = 40,
            Font = new Font("Consolas", 18f, FontStyle.Bold),
            ReadOnly = true, BackColor = Color.FromArgb(245, 245, 245),
            BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center
        };
        Controls.Add(txtPassword);

        //Phonetic Cheat Sheet Button calling our new standalone class!
        btnPhonetic = new Button { Text = "A-Z", Left = 295, Top = 79, Width = 55, Height = 36, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand, FlatStyle = FlatStyle.Flat };
        btnPhonetic.FlatAppearance.BorderColor = Color.Silver;
        btnPhonetic.Click += (_, _) => PhoneticSpeller.Show(txtPassword.Text, this);
        Controls.Add(btnPhonetic);

        //Copy Button
        btnCopy = new Button { Text = "📋", Left = 355, Top = 79, Width = 65, Height = 36, Font = new Font("Segoe UI", 12f), Cursor = Cursors.Hand, FlatStyle = FlatStyle.Flat };
        btnCopy.FlatAppearance.BorderColor = Color.Silver;
        btnCopy.Click += (_, _) => { if (!string.IsNullOrEmpty(txtPassword.Text)) { Clipboard.SetText(txtPassword.Text); btnCopy.Text = "✅"; var t = new System.Windows.Forms.Timer { Interval = 1500 }; t.Tick += (s, ev) => { btnCopy.Text = "📋"; t.Stop(); t.Dispose(); }; t.Start(); } };
        Controls.Add(btnCopy);

        //Exact Counts Configuration Group
        var grp = new GroupBox { Text = "Exact Character Counts", Left = 20, Top = 135, Width = 400, Height = 130, Font = new Font("Segoe UI", 9f) };
        
        grp.Controls.Add(new Label { Text = "Total Length:", Left = 15, Top = 30, Width = 80 });
        numLength = new NumericUpDown { Left = 100, Top = 28, Width = 60, Minimum = 4, Maximum = 128, Value = Math.Max(4, Math.Min(128, defaults.DefaultLength)) };
        grp.Controls.Add(numLength);

        grp.Controls.Add(new Label { Text = "Capitals (A-Z):", Left = 15, Top = 65, Width = 85 });
        numUpper = new NumericUpDown { Left = 100, Top = 63, Width = 60, Minimum = 0, Maximum = 128, Value = Math.Max(0, Math.Min(128, defaults.DefaultUpper)) };
        grp.Controls.Add(numUpper);

        grp.Controls.Add(new Label { Text = "Numbers (0-9):", Left = 200, Top = 30, Width = 95 });
        numNumbers = new NumericUpDown { Left = 300, Top = 28, Width = 60, Minimum = 0, Maximum = 128, Value = Math.Max(0, Math.Min(128, defaults.DefaultNumbers)) };
        grp.Controls.Add(numNumbers);

        grp.Controls.Add(new Label { Text = "Symbols (@#$):", Left = 200, Top = 65, Width = 95 });
        numSymbols = new NumericUpDown { Left = 300, Top = 63, Width = 60, Minimum = 0, Maximum = 128, Value = Math.Max(0, Math.Min(128, defaults.DefaultSymbols)) };
        grp.Controls.Add(numSymbols);

        Controls.Add(grp);

        //SpeakEasy Mode Toggles
        chkSpeakEasy = new CheckBox { Text = "🗣️ SpeakEasy Mode (LeetSpeak substitutions)", Left = 20, Top = 275, Width = 400, Checked = defaults.DefaultSpeakEasy, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.DarkGreen };
        Controls.Add(chkSpeakEasy);

        chkPhrases = new CheckBox { Text = "📝 Use Hyphens for Memorable Phrases (e.g. 4pple-Dr@gon)", Left = 40, Top = 295, Width = 380, Checked = false, Font = new Font("Segoe UI", 9f), ForeColor = Color.DimGray };
        Controls.Add(chkPhrases);

        //Link the phrase checkbox so it disables if SpeakEasy is off
        chkSpeakEasy.CheckedChanged += (_, _) => chkPhrases.Enabled = chkSpeakEasy.Checked;
        chkPhrases.Enabled = chkSpeakEasy.Checked;

        //Generate Button
        btnGenerate = new Button { Text = "Generate Password", Left = 20, Top = 330, Width = 400, Height = 36, Font = new Font("Segoe UI", 10f, FontStyle.Bold), BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnGenerate.FlatAppearance.BorderSize = 0;
        btnGenerate.Click += (_, _) => GeneratePassword();
        Controls.Add(btnGenerate);
    }

    private async Task EnsureDictionaryCachedAsync()
    {
        if (File.Exists(_dictPath) && new FileInfo(_dictPath).Length > 0) return;

        try
        {
            using var client = new HttpClient();
            string url = "https://raw.githubusercontent.com/first20hours/google-10000-english/master/google-10000-english-no-swears.txt";
            string rawText = await client.GetStringAsync(url);

            var words = rawText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                               .Where(w => w.Length >= 4 && w.Length <= 8).ToArray();
            File.WriteAllLines(_dictPath, words);
        }
        catch
        {
            var fallbackWords = new[] { "apple", "brave", "change", "delta", "eagle", "falcon", "giant", "hero", "jungle", "rocket" };
            File.WriteAllLines(_dictPath, fallbackWords);
        }
    }

    private string[] ReadDictionary()
    {
        if (File.Exists(_dictPath))
        {
            var lines = File.ReadAllLines(_dictPath).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim().ToLower()).ToArray();
            if (lines.Length > 0) return lines;
        }
        return new[] { "panda" }; 
    }

    private void GeneratePassword()
    {
        int length = (int)numLength.Value;
        int upper = (int)numUpper.Value;
        int numbers = (int)numNumbers.Value;
        int symbols = (int)numSymbols.Value;

        if (upper + numbers + symbols > length)
        {
            MessageBox.Show("The sum of Capitals, Numbers, and Symbols cannot exceed the Total Length!", "PandaPassGen Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (chkSpeakEasy.Checked)
        {
            txtPassword.Text = GenerateSpeakEasy(length, upper, numbers, symbols, chkPhrases.Checked);
        }
        else
        {
            txtPassword.Text = GenerateStandard(length, upper, numbers, symbols);
        }
    }

    private string GenerateSpeakEasy(int length, int upperCount, int numCount, int symCount, bool usePhrases)
    {
        var dict = ReadDictionary();
        var words = new List<string>();
        int currentLength = 0;
        var firstLetters = new List<int>();

        //######################################
        //Step 1 Build base words and track their starting indices
        //######################################
        while (currentLength < length)
        {
            string w = dict[GetRandomInt(dict.Length)];
            firstLetters.Add(currentLength);
            words.Add(w);
            currentLength += w.Length + (usePhrases ? 1 : 0);
        }

        string rawString = usePhrases ? string.Join("-", words) : string.Join("", words);
        
        //If exact length format is requested, chop the excess characters!
        if (!usePhrases && rawString.Length > length) 
            rawString = rawString.Substring(0, length);
        
        char[] pwd = rawString.ToCharArray();

        List<int> availableIndices = new List<int>();
        for (int i = 0; i < pwd.Length; i++)
        {
            if (pwd[i] != '-') availableIndices.Add(i);
        }

        int PullRandomIndex()
        {
            if (availableIndices.Count == 0) return 0;
            int idx = GetRandomInt(availableIndices.Count);
            int val = availableIndices[idx];
            availableIndices.RemoveAt(idx);
            return val;
        }

        //######################################
        //Step 2 Inject EXACTLY numCount Symbols
        //######################################
        string fallbackSyms = "!@#$&-+=";
        for (int i = 0; i < symCount; i++)
        {
            if (availableIndices.Count == 0) break;
            int idx = PullRandomIndex();
            char c = char.ToLower(pwd[idx]);
            if (c == 'a') pwd[idx] = '@';
            else if (c == 's') pwd[idx] = '$';
            else if (c == 'i') pwd[idx] = '!';
            else pwd[idx] = fallbackSyms[GetRandomInt(fallbackSyms.Length)];
        }

        //######################################
        //Step 3 Inject EXACTLY numCount Numbers
        //######################################
        string fallbackNums = "0123456789";
        for (int i = 0; i < numCount; i++)
        {
            if (availableIndices.Count == 0) break;
            int idx = PullRandomIndex();
            char c = char.ToLower(pwd[idx]);
            if (c == 'e') pwd[idx] = '3';
            else if (c == 'a') pwd[idx] = '4';
            else if (c == 't') pwd[idx] = '7';
            else if (c == 'o') pwd[idx] = '0';
            else if (c == 'l' || c == 'i') pwd[idx] = '1';
            else if (c == 's') pwd[idx] = '5';
            else pwd[idx] = fallbackNums[GetRandomInt(fallbackNums.Length)];
        }

        //######################################
        //Step 4 Inject EXACTLY upperCount Capitals
        //######################################
        for (int i = 0; i < upperCount; i++)
        {
            if (availableIndices.Count == 0) break;
            int targetIdx = -1;

            foreach (var fl in firstLetters) {
                if (fl < pwd.Length && availableIndices.Contains(fl) && char.IsLower(pwd[fl])) {
                    targetIdx = fl;
                    break;
                }
            }

            if (targetIdx != -1) {
                availableIndices.Remove(targetIdx);
            } else {
                targetIdx = PullRandomIndex();
            }

            pwd[targetIdx] = char.ToUpper(pwd[targetIdx]);
        }

        return new string(pwd);
    }

    private string GenerateStandard(int length, int upperCount, int numCount, int symCount)
    {
        string uppers = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string lowers = "abcdefghijklmnopqrstuvwxyz";
        string numbers = "0123456789";
        string symbols = "!@#$%^&*-_+=";

        int lowerCount = length - (upperCount + numCount + symCount);
        var charList = new List<char>();

        for (int i = 0; i < upperCount; i++) charList.Add(uppers[GetRandomInt(uppers.Length)]);
        for (int i = 0; i < numCount; i++) charList.Add(numbers[GetRandomInt(numbers.Length)]);
        for (int i = 0; i < symCount; i++) charList.Add(symbols[GetRandomInt(symbols.Length)]);
        for (int i = 0; i < lowerCount; i++) charList.Add(lowers[GetRandomInt(lowers.Length)]);

        for (int i = charList.Count - 1; i > 0; i--)
        {
            int j = GetRandomInt(i + 1);
            var temp = charList[i];
            charList[i] = charList[j];
            charList[j] = temp;
        }

        return new string(charList.ToArray());
    }

    private int GetRandomInt(int maxExclusive)
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[4];
        rng.GetBytes(bytes);
        uint scale = BitConverter.ToUInt32(bytes, 0);
        return (int)(maxExclusive * (scale / (uint.MaxValue + 1.0)));
    }
}
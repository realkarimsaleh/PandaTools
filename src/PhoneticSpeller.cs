using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

public static class PhoneticSpeller
{
    public static void Show(string text, IWin32Window? owner = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        //######################################
        //Dynamic Height Calculation
        //######################################
        int rowHeight = 24;
        int headerHeight = 60; // Space for the password display at the top
        int footerHeight = 55; // Space for the copy button at the bottom
        
        //Calculate grid height based on password length (rows + column header + border padding)
        int calculatedGridHeight = (text.Length * rowHeight) + 32; 
        
        //Cap the grid height at 450px so it doesn't disappear off the screen for massive passwords
        int actualGridHeight = Math.Min(calculatedGridHeight, 450); 
        
        int totalClientHeight = headerHeight + actualGridHeight + footerHeight;

        using var frm = new Form
        {
            Text = "NATO Phonetic Spelling",
            ClientSize = new Size(385, totalClientHeight), // ClientSize gives us exact pixel control inside the window
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
            ShowIcon = false
        };

        //######################################
        //Password Display (Top)
        //######################################
        var txtPass = new TextBox
        {
            Text = text,
            Font = new Font("Consolas", 14f, FontStyle.Bold),
            ReadOnly = true,
            BackColor = Color.FromArgb(245, 245, 245),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center,
            Top = 15,
            Left = 20,
            Width = 345,
            Height = 32
        };
        frm.Controls.Add(txtPass);

        //######################################
        //DataGridView Table (Middle)
        //######################################
        var grid = new DataGridView
        {
            Top = 60,
            Left = 20,
            Width = 345,
            Height = actualGridHeight,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeColumns = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, // Clean, modern horizontal lines
            GridColor = Color.LightGray,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Font = new Font("Segoe UI", 10f)
        };

        //Define Table Columns
        grid.Columns.Add("Char", "Char");
        grid.Columns[0].Width = 50;
        grid.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns[0].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        grid.Columns.Add("Type", "Type");
        grid.Columns[1].Width = 100;
        grid.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns[1].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        grid.Columns.Add("Phonetic", "Phonetic");
        grid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        //######################################
        // DISABLE SORTING (The Fix!)
        //######################################
        foreach (DataGridViewColumn col in grid.Columns)
        {
            col.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        //Style the Table Header
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        grid.ColumnHeadersHeight = 30;

        //######################################
        //Populate Table Data & Colors
        //######################################
        var sbCopy = new StringBuilder();
        sbCopy.AppendLine($"Password: {text}");
        sbCopy.AppendLine(new string('-', 40));

        foreach (char c in text)
        {
            string caseType = char.IsLetter(c) ? (char.IsUpper(c) ? "Uppercase" : "Lowercase") : "Symbol";
            if (char.IsDigit(c)) caseType = "Number";
            
            string phonetic = GetPhonetic(c);

            int rowIndex = grid.Rows.Add(c.ToString(), caseType, phonetic);
            var row = grid.Rows[rowIndex];
            row.Height = rowHeight;

            //Color-coding for quick visual scanning
            if (char.IsUpper(c)) row.DefaultCellStyle.ForeColor = Color.DarkBlue;
            else if (char.IsDigit(c) || !char.IsLetterOrDigit(c)) row.DefaultCellStyle.ForeColor = Color.DarkGreen;
            else row.DefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);

            sbCopy.AppendLine($"{c}  -  {caseType,-10}  -  {phonetic}");
        }

        //Clear default selection so it looks clean upon opening
        grid.DataBindingComplete += (_, _) => grid.ClearSelection();
        frm.Controls.Add(grid);

        //######################################
        //Copy Button (Bottom)
        //######################################
        var btnCopy = new Button
        {
            Text = "📋 Copy to Clipboard",
            Top = totalClientHeight - 45, // Pin to the bottom padding
            Left = 20,
            Width = 345,
            Height = 32,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(245, 245, 245)
        };
        btnCopy.FlatAppearance.BorderColor = Color.Silver;
        btnCopy.Click += (_, _) => {
            Clipboard.SetText(sbCopy.ToString());
            btnCopy.Text = "✅ Copied!";
            btnCopy.BackColor = Color.FromArgb(40, 167, 69);
            btnCopy.ForeColor = Color.White;
        };
        frm.Controls.Add(btnCopy);

        if (owner != null) frm.ShowDialog(owner);
        else frm.ShowDialog();
    }

    private static string GetPhonetic(char c) => char.ToLowerInvariant(c) switch
    {
        'a' => "Alpha", 
        'b' => "Bravo",
        'c' => "Charlie",
        'd' => "Delta",
        'e' => "Echo",
        'f' => "Foxtrot",
        'g' => "Golf",
        'h' => "Hotel",
        'i' => "India",
        'j' => "Juliet",
        'k' => "Kilo",
        'l' => "Lima",
        'm' => "Mike",
        'n' => "November",
        'o' => "Oscar",
        'p' => "Papa",
        'q' => "Quebec",
        'r' => "Romeo",
        's' => "Sierra",
        't' => "Tango",
        'u' => "Uniform",
        'v' => "Victor",
        'w' => "Whiskey",
        'x' => "X-ray",
        'y' => "Yankee",
        'z' => "Zulu",
        '0' => "Zero",
        '1' => "One",
        '2' => "Two", 
        '3' => "Three",
        '4' => "Four",
        '5' => "Five",
        '6' => "Six",
        '7' => "Seven",
        '8' => "Eight",
        '9' => "Nine",
        '!' => "Exclamation Mark",
        '@' => "At Sign",
        '#' => "Hash / Pound",
        '$' => "Dollar Sign",
        '%' => "Percent",
        '^' => "Caret",
        '&' => "Ampersand",
        '*' => "Asterisk",
        '-' => "Hyphen / Dash",
        '_' => "Underscore",
        '+' => "Plus",
        '=' => "Equals",
        _ => "Unknown"
    };
}
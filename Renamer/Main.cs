﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using MetroFramework.Controls;
using Renamer.Common;
using Renamer.Models;

namespace Renamer
{
    public partial class Main : Form
    {
        private string[] fileList;

        private List<FileName> fileNames = new List<FileName>();
        private List<FileName> previousNames = new List<FileName>();

        private List<Filter> filterList = new List<Filter>();
        private ProfileManager profileManager;

        private List<string[]> errorList = new List<string[]>();

        #region GUI

        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {



            this.MinimumSize = this.Size;
            Main_Resize(null, null);

            //dropDownSort.SelectedIndex = 0;

            profileManager = new ProfileManager(dropDownProfile);
            profileManager.PopulateProfiles();

            naturalSortToolStripMenuItem.Checked = true;
            //olvPreview.Columns[0].

            //Set colors to textBoxFilter
            SetFocusColor(textBoxFilter);

            //Load regex filters
            //RegexFilters.SetData(contextMenuRegex, textBoxFilter, textBoxFilter_TextChanged)
            RegexHelper.SetData(contextMenuRegex, textBoxFilter);
        }

        private void SetFocusColor(TextBox textBox)
        {
            textBox.GotFocus += (sender, e) => { textBox.ForeColor = Color.Black; };
            textBox.LostFocus += (sender, e) => { textBox.ForeColor = Color.Gray; };
        }

        private void Main_Resize(object sender, EventArgs e)
        {
            int width;

            if (olvPreview.Columns.Count == 2)
            {
                width = olvPreview.Width / 2 - 15;
                olvPreview.Columns[0].Width = width;
                olvPreview.Columns[1].Width = width;
            }

            //if (olvFilters.Columns.Count != 3) return;
            width = olvFilters.Width - olvFilters.Columns[1].Width - olvFilters.Columns[2].Width - 25;
            if (width > 0) olvFilters.Columns[0].Width = width;
        }

        private void buttonBrowseInput_Click(object sender, EventArgs e)
        {
            if (folderBrowser.ShowDialog() != DialogResult.OK) return;
            textBoxInputDir.Text = folderBrowser.SelectedPath;

            OnInputDirChange();
        }

        private void OnInputDirChange()
        {
            buttonRevert.Enabled = false;
            if (checkBoxSame.Checked) textBoxOutput.Text = textBoxInputDir.Text;
            LoadFiles();
        }

        private void buttonBrowseOutput_Click(object sender, EventArgs e)
        {
            if (folderBrowser.ShowDialog() != DialogResult.OK) return;
            textBoxOutput.Text = folderBrowser.SelectedPath;

            OnOutputDirChange();
        }

        private void OnOutputDirChange()
        {
            buttonRevert.Enabled = false;
        }

        private void checkBoxSame_CheckedChanged(object sender, EventArgs e)
        {
            if (((MetroCheckBox)sender).Checked)
            {
                textBoxOutput.Text = textBoxInputDir.Text;
                buttonBrowseOutput.Enabled = false;
            }
            else buttonBrowseOutput.Enabled = true;
        }

        private void textBoxFilter_TextChanged(object sender, EventArgs e)
        {
            //if (!Directory.Exists(textBoxInputDir.Text)) return;
            if (textBoxInputDir.Text == "")
            {
                //these are the same three lines inside ApplyFileNameFilter()
                //but they're never reached if the input dir is empty
                textBoxFilter.BackColor = SystemColors.Window;
                if (!StringFunctions.ValidPattern(textBoxFilter.Text))
                    textBoxFilter.BackColor = Color.MistyRose;

                return;
            }

            buttonRevert.Enabled = false;
            LoadFiles();
        }

        #endregion

        void SortFileList()
        {
            if (fileList.Length == 0) return;            

            switch (sortMethod)
            {                   
                case 0: //Natural
                    Array.Sort(fileList, new Common.Comparers.Natural());
                    break;

                case 1: //Natural Descending                     
                    Array.Sort(
                        fileList,
                        new Common.Comparers.Natural(Common.Comparers.NaturalComparerOptions.Default, Common.Comparers.NaturalComparerDirection.Descending)
                        );
                    break;

                case 2: //Alphanumerical
                    Array.Sort(fileList);
                    break;

                case 3: //Alphanumerical Descending
                    Array.Sort(fileList, new Common.Comparers.Descending());
                    break;
            }
        }

        void LoadFiles()
        {
            //Get files from specified path, it's necessary to sort them because GetFiles() returns an unsorted array on network drives            
            fileList = Directory.GetFiles(textBoxInputDir.Text);

            SortFileList();
            ApplyFileNameFilter();
            ApplyFiltersAndUpdate();

            totalFiles.Text = fileList.Length + " Files";
            filesFound.Text = fileNames.Count + " Files";
        }

        void ApplyFileNameFilter()
        {
            fileNames.Clear();
            textBoxFilter.BackColor = SystemColors.Window;

            if (textBoxFilter.Text.Length > 0)
            {
                foreach (var file in fileList)
                {
                    if (!StringFunctions.ValidPattern(textBoxFilter.Text))
                    {
                        textBoxFilter.BackColor = Color.MistyRose;
                        break;
                    }

                    string name = Path.GetFileName(file);

                    //if (name.ToLower().Contains(textBoxFilter.Text.ToLower()))
                    //    fileNames.Add(new FileName(Path.GetDirectoryName(file), name, name));

                    //Use RegEx match
                    if (name.RegexMatch(textBoxFilter.Text))
                        fileNames.Add(new FileName(Path.GetDirectoryName(file), name, name));

                }
            }
            else
            {
                foreach (var file in fileList)
                {
                    //Console.WriteLine(file);
                    string name = Path.GetFileName(file);
                    fileNames.Add(new FileName(Path.GetDirectoryName(file), name, name));
                }
            }
        }

        void ApplyFilterList(List<Filter> list)
        {
            for (int i = 0; i < fileNames.Count; i++)
            {
                fileNames[i].Reset();

                foreach (var filter in list)
                {
                    fileNames[i].Modified = filter.ApplyTo(fileNames[i], i, fileNames.Count);
                }
            }
        }

        void PreviewFilter(FilterType type, object x = null, object y = null)
        {
            var temp = new List<Filter>(filterList);
            temp.Add(new Filter(type, x, y));

            ApplyFilterList(temp);

            olvPreview.SetObjects(fileNames);
        }

        void ApplyFiltersAndUpdate()
        {
            ScrollDownFilters();

            ApplyFilterList(filterList);
            olvPreview.SetObjects(fileNames);
        }

        //Make last element from filters preview visible
        void ScrollDownFilters()
        {
            if (olvFilters.Items.Count > 0)
            {
                int lastIndex = olvFilters.Items.Count - 1;
                olvFilters.Items[lastIndex].EnsureVisible();
                olvFilters.SelectedIndex = lastIndex;
            }
        }

        //Only for filters without arguments
        void ApplySimpleFilter(FilterType filterType)
        {
            filterList.Add(new Filter(filterType));
            olvFilters.SetObjects(filterList);

            ApplyFiltersAndUpdate();
        }

        void AddFilter(Filter filter)
        {
            filterList.Add(filter);
            olvFilters.SetObjects(filterList);
        }

        void ResetNumericUpDown(NumericUpDown numericUpDown)
        {
            numericUpDown.Value = 0;
            numericUpDown.Minimum = 0;
        }

        //Evaluate dialog for Filters with 1 numeric argument (the blue rows on the excel file)
        void EvalDialog_Num(string title, string prompt, FilterType filterType)
        {
            var dlg = new Dialogs.Number(title, prompt, this);
            dlg.inputNumber.ValueChanged += (o, args) => PreviewFilter(filterType, dlg.inputNumber.Value);

            ResetNumericUpDown(dlg.inputNumber);

            if (dlg.ShowDialog() == DialogResult.OK)
                AddFilter(new Filter(filterType, dlg.inputNumber.Value));

            ApplyFiltersAndUpdate();
        }

        //Evaluate dialog for Filters with 1 string argument (the purple rows on the excel file)
        void EvalDialog_Str(string title, string prompt, FilterType filterType)
        {
            var dlg = new Dialogs.String(title, prompt, this);
            dlg.inputText.TextChanged += (o, args) => PreviewFilter(filterType, dlg.inputText.Text);

            if (dlg.ShowDialog() == DialogResult.OK)
                AddFilter(new Filter(filterType, dlg.inputText.Text));

            ApplyFiltersAndUpdate();
        }

        //Evaluate dialog for Filters with 2 string arguments (the cyan rows on the excel file)
        void EvalDialog_Str_Str(string title, string prompt1, string prompt2, FilterType filterType, string searchString = null)
        {
            var dlg = new Dialogs.StringString(title, prompt1, prompt2, this);
            dlg.inputText1.TextChanged += (sender, args) => PreviewFilter(filterType, dlg.inputText1.Text, dlg.inputText2.Text);
            dlg.inputText2.TextChanged += (sender, args) => PreviewFilter(filterType, dlg.inputText1.Text, dlg.inputText2.Text);

            if (searchString != null)
            {
                dlg.inputText1.Text = searchString;
                dlg.inputText2.Text = searchString;
            }

            if (dlg.ShowDialog() == DialogResult.OK)
                AddFilter(new Filter(filterType, dlg.inputText1.Text, dlg.inputText2.Text));

            ApplyFiltersAndUpdate();
        }

        #region Menu Items

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.Clear);
        }

        private void addNumberingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Num("Add Numbering", "Position:", FilterType.AddNumbering);
        }

        private void swapOrderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Num("Swap Order", "Position:", FilterType.SwapOrder);
        }

        private void appendBeforeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Str("Append Before", "Text:", FilterType.AppendBefore);
        }

        private void appendAfterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Str("Append After", "Text:", FilterType.AppendAfter);
        }

        private void appendAtPositionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new Dialogs.NumberString("Append at Position", "Position:", "Text:", this);
            dlg.inputNumber.ValueChanged += (o, args) => PreviewFilter(FilterType.AppendAtPosition, dlg.inputText.Text, dlg.inputNumber.Value);
            dlg.inputText.TextChanged += (o, args) => PreviewFilter(FilterType.AppendAtPosition, dlg.inputText.Text, dlg.inputNumber.Value);

            if (dlg.ShowDialog() == DialogResult.OK)
                AddFilter(new Filter(FilterType.AppendAtPosition, dlg.inputText.Text, dlg.inputNumber.Value));

            ApplyFiltersAndUpdate();
        }

        private void appendFromTextFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new Dialogs.NumberFile("Append from Text File", "Position:", "Text File:", this);
            dlg.fileDialog.Filter = @"Text Files (*.txt)|*.txt|All Files|*.*";
            dlg.inputNumber.ValueChanged += (o, args) => PreviewFilter(FilterType.AppendFromTextFile, dlg.inputFile.Text, dlg.inputNumber.Value);
            dlg.inputFile.TextChanged += (o, args) => PreviewFilter(FilterType.AppendFromTextFile, dlg.inputFile.Text, dlg.inputNumber.Value);

            if (dlg.ShowDialog() == DialogResult.OK)
                AddFilter(new Filter(FilterType.AppendFromTextFile, dlg.inputFile.Text, dlg.inputNumber.Value));

            ApplyFiltersAndUpdate();
        }

        private void extractNumbersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.KeepNumeric);
        }

        private void keepAlphanumericCharactersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.KeepAlphanumeric);
        }

        private void removeInvalidCharactersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.RemoveInvalidCharacters);
        }

        private void preserveFromLeftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Num("Preserve from Left", "Count:", FilterType.PreserveFromLeft);
        }

        private void preserveFromRightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Num("Preserve from Right", "Count:", FilterType.PreserveFromRight);
        }

        private void trimFromLeftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Num("Trim from Left", "Count:", FilterType.TrimFromLeft);
        }

        private void trimFromRightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Num("Trim from Right", "Count:", FilterType.TrimFromRight);
        }

        private void capitalizeEachWordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.CapitalizeEachWord);
        }

        private void toUppercaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.UpperCase);
        }

        private void toLowercaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.LowerCase);
        }

        private void toSentenceCaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.SentenceCase);
        }

        private void regularExpressionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Str("Regular Expression", "Expression:", FilterType.Regex);
        }

        private void regexReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Str_Str("Regex Replace", "Expression:", "Replace String:", FilterType.RegexReplace);
        }

        private void replaceStringToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Str_Str("Replace String", "Search String:", "Replace String:", FilterType.ReplaceString);
        }

        private void replaceStringCaseInsensitiveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EvalDialog_Str_Str("Replace String (Case Insensitive)", "Search String:", "Replace String:", FilterType.ReplaceCaseInsensitive);
        }

        private void addExtensionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.AddExtension);
        }

        private void removeExtensionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ApplySimpleFilter(FilterType.RemoveExtension);
        }

        #endregion

        #region Buttons

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            //Duplicate last filter
            if (filterList.Count > 0)
            {
                AddFilter(filterList[filterList.Count - 1]);
                ApplyFiltersAndUpdate();
            }
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            //Delete selected filter
            if (filterList.Count > 0 && olvFilters.SelectedIndices.Count == 1)
            {
                filterList.RemoveAt(olvFilters.SelectedIndex);

                olvFilters.SetObjects(filterList);
                ApplyFiltersAndUpdate();

                if (filterList.Count > 0)
                {
                    olvFilters.SelectedIndex = filterList.Count - 1;
                }
            }
        }

        private void deleteSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonDelete_Click(null, null);
        }

        private void removeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            filterList.Clear();

            olvFilters.SetObjects(filterList);
            ApplyFiltersAndUpdate();
        }

        private void buttonUp_Click(object sender, EventArgs e)
        {
            if (filterList.Count > 1 && olvFilters.SelectedIndices.Count == 1)
            {
                int index = olvFilters.SelectedIndex;
                var filter = filterList[index];
                if (index > 0)
                {
                    filterList.RemoveAt(index);
                    index--;
                    filterList.Insert(index, filter);

                    olvFilters.SetObjects(filterList);
                    ApplyFiltersAndUpdate();

                    olvFilters.SelectedIndex = index;
                }
            }
        }

        private void buttonDown_Click(object sender, EventArgs e)
        {
            if (filterList.Count > 1 && olvFilters.SelectedIndices.Count == 1)
            {
                int index = olvFilters.SelectedIndex;
                var filter = filterList[index];
                if (index < filterList.Count - 1)
                {
                    filterList.RemoveAt(index);
                    index++;
                    filterList.Insert(index, filter);

                    olvFilters.SetObjects(filterList);
                    ApplyFiltersAndUpdate();

                    olvFilters.SelectedIndex = index;
                }
            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            //var result = MessageBox.Show("Are you sure you want to exit?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            //if (result == DialogResult.No) e.Cancel = true;
        }

        #endregion

        #region Profile Methods

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (dropDownProfile.SelectedIndex == -1) saveAsToolStripMenuItem_Click(null, null);
            else profileManager.Update(filterList);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonSave_Click(null, null);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new Dialogs.String("Save Profile", "Name:");
            var result = dlg.ShowDialog();

            while (true)
            {
                //If user pressed OK button
                if (result == DialogResult.OK)
                {
                    //If profile was saved successfully
                    if (profileManager.AddProfile(new Profile(dlg.inputText.Text, filterList)))
                    {
                        break;
                    }
                }

                //If user pressed Cancel button
                if (result == DialogResult.Cancel) break;

                result = dlg.ShowDialog();
            }


            //if (dlg.ShowDialog() == DialogResult.OK)
            //    profileManager.AddProfile(new Profile(dlg.inputText.Text, filterList));
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dropDownProfile.SelectedIndex == -1) return;

            var dlg = new Dialogs.String("Rename Profile", "Name:");
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                if (dlg.inputText.Text == "") return;

                profileManager.DeleteSelected();
                profileManager.AddProfile(new Profile(dlg.inputText.Text, filterList));
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dropDownProfile.SelectedIndex == -1) return;

            var result = MessageBox.Show("Are you sure you want to delete the selected profile?", "Delete Profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
                profileManager.DeleteSelected();
        }

        private void dropDownProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            profileManager.AssignProfile(out filterList);

            olvFilters.SetObjects(filterList);
            ApplyFiltersAndUpdate();
        }

        #endregion

        private void olvPreview_FormatRow(object sender, BrightIdeasSoftware.FormatRowEventArgs e)
        {
            var current = (FileName)e.Model;
            if (!current.IsValidName())
                e.Item.BackColor = Color.FromArgb(255, 41, 56);
        }

        #region Rename and Revert Buttons

        void FileRename(FileName name)
        {
            //calculate source and destination files
            string src = textBoxInputDir.Text + "\\" + name.Original;
            string dst = textBoxOutput.Text + "\\" + name.Modified;

            try
            {
                //move here
                if (textBoxInputDir.Text == textBoxOutput.Text) File.Move(src, dst);
                //copy if directories are different
                else File.Copy(src, dst);
            }
            catch (Exception ex)
            {
                errorList.Add(new string[] { name.Modified, ex.Message });
            }
        }

        void Rename()
        {
            errorList.Clear();
            var dlg = new Windows.Progress("Rename in Progress...");

            //CheckForIllegalCrossThreadCalls = false;
            //stackoverflow.com/questions/661561/how-to-update-the-gui-from-another-thread-in-c?rq=1
            //never use a loop inside a method invoker

            dlg.bgWorker.DoWork += (o, args) =>
            {
                double step = 100.0 / fileNames.Count;
                double sum = 0;

                foreach (var name in fileNames)
                {
                    if (dlg.wantToCancel) break;

                    this.Invoke((MethodInvoker)delegate
                    {
                        dlg.labelCurrent.Text = name.Modified;
                        FileRename(name);

                        sum += step;
                        dlg.barProgress.Value = (int)sum;
                    });
                }

                this.Invoke((MethodInvoker)delegate
                {
                    dlg.barProgress.Value = 100;
                    dlg.wantToCancel = true;
                    dlg.Close();

                    if (errorList.Count > 0)
                    {
                        var errorDlg = new Windows.Errors("Error", "Cannot rename some files, please review the errors below", errorList);
                        errorDlg.ShowDialog();
                    }
                });

                //if input and output directories are the same, reload files
                if (textBoxInputDir.Text == textBoxOutput.Text) LoadFiles();
            };

            dlg.bgWorker.RunWorkerAsync();
            dlg.ShowDialog();
        }

        void BackupFileNames()
        {
            previousNames.Clear();

            foreach (var name in fileNames)
                previousNames.Add(new FileName(name, true));

            if (textBoxInputDir.Text == textBoxOutput.Text)
                buttonRevert.Enabled = true;
        }

        private void buttonRename_Click(object sender, EventArgs e)
        {
            if (textBoxInputDir.Text == "" || textBoxOutput.Text == "") return;
            if (fileNames.Count == 0 || filterList.Count == 0) return;

            bool allNamesAreValid = fileNames.All(name => name.IsValidName());
            if (allNamesAreValid)
            {
                bool allNamesAreUnique = fileNames.Select(x => x.Modified).Count() ==
                                         fileNames.Select(x => x.Modified).Distinct().Count();

                if (!allNamesAreUnique)
                {
                    var result = MessageBox.Show("The file names must be unique, are you sure you want to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result == DialogResult.No) return;
                }

                BackupFileNames();
                Rename();
            }
            else MessageBox.Show("Some file names are not valid, please correct them before continue.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void buttonRevert_Click(object sender, EventArgs e)
        {
            buttonRevert.Enabled = false;
            fileNames = new List<FileName>(previousNames);
            Rename();
        }

        #endregion

        #region Drag and Drop
        //Drag and drop folders
        //stackoverflow.com/questions/7189779/drag-and-drop-a-folder-from-windows-explorer-to-listbox-in-c-sharp
        //stackoverflow.com/questions/1395205/better-way-to-check-if-path-is-a-file-or-a-directory

        private void textBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
            else e.Effect = DragDropEffects.None;
        }

        private string GetFirstPath(DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            var attr = File.GetAttributes(paths[0]);

            if ((attr & FileAttributes.Directory) == FileAttributes.Directory) return paths[0];
            return "";
        }

        private void textBoxInputDir_DragDrop(object sender, DragEventArgs e)
        {
            string path = GetFirstPath(e);

            if (path != "")
            {
                textBoxInputDir.Text = path;

                OnInputDirChange();
            }
        }

        private void textBoxOutput_DragDrop(object sender, DragEventArgs e)
        {
            string path = GetFirstPath(e);

            if (path != "")
            {
                textBoxOutput.Text = path;

                OnOutputDirChange();
                checkBoxSame.Checked = false;
            }
        }

        #endregion

        private void dropDownSort_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (fileList == null || fileNames.Count == 0) return;

            SortFileList();
            ApplyFileNameFilter();
            ApplyFiltersAndUpdate();
        }

        /*
         * 0: Natural
         * 1: Natural Descending
         * 2: Alphanumerical
         * 3: Alphanumerical Descending
         */
        byte sortMethod = 0;

        private void olvPreview_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != 0) return;
            contextMenuSort.Show(Cursor.Position);
        }

        void UncheckSortMenuItems()
        {
            naturalSortToolStripMenuItem.Checked = false;
            naturalDescendingSortToolStripMenuItem.Checked = false;
            alphanumericalSortToolStripMenuItem.Checked = false;
            alphanumericalDescendingSortToolStripMenuItem.Checked = false;
        }

        /*
         * sender: The menu item
         * method: Sort method, see above
         */
        void OnSortMenuItemClick(object sender, byte method)
        {
            UncheckSortMenuItems();
            var thisItem = sender as ToolStripMenuItem;
            thisItem.Checked = true;

            sortMethod = method;

            if (fileList == null || fileNames.Count == 0) return;

            SortFileList();
            ApplyFileNameFilter();
            ApplyFiltersAndUpdate();
        }

        private void naturalSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnSortMenuItemClick(sender, 0);
        }

        private void naturalDescendingSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnSortMenuItemClick(sender, 1);
        }

        private void alphanumericalSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnSortMenuItemClick(sender, 2);
        }

        private void alphanumericalDescendingSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OnSortMenuItemClick(sender, 3);
        }
        
        private void refreshButton_Click(object sender, EventArgs e)
        {
            textBoxFilter_TextChanged(null, null);
        }

        private void buttonRegex_Click(object sender, EventArgs e)
        {
            textBoxFilter.Clear();
            textBoxFilter.Focus();
        }

        private void olvPreview_DoubleClick(object sender, EventArgs e)
        {
            var fn = (FileName)olvPreview.SelectedObject;
            EvalDialog_Str_Str("Replace String", "Search String:", "Replace String:", FilterType.ReplaceString, fn.Modified);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new Windows.About().ShowDialog();
        }

        private void copyAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string data = "";

            foreach (var row in olvPreview.Objects)
                data += (row as FileName).Original + "\r\n";            

            Clipboard.SetText(data);
        }

        //private void parentDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    EvalDialog_Num("Parent Directory", "Position:", FilterType.ParentDirectory);
        //}

        

      

       

    
        












    }
}
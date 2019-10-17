using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Octokit;
using GitHubApiDemo.Properties;
using System.Xml;
using System.Threading.Tasks;

namespace GitHubApiDemo
{
    /// <summary>
    /// The main window for this application.
    /// </summary>
    public partial class MainForm : Form
    {
        #region Constructors
        /// <summary>
        /// Create an instance of this form.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();

            mainDataGridView.AutoGenerateColumns = false;

            // Trick to make read-only properties display using regular text color
            // See: https://social.msdn.microsoft.com/Forums/windows/en-US/9fd7591d-8925-43e4-bdf1-988c9bb5ca5e/changing-font-color-on-readonly-fields-in-propertygrid?forum=winforms
            detailPropertyGrid.ViewForeColor = Color.FromArgb(0, 0, 1);
        }
        #endregion // Constructors

        #region Constants
        /// <summary>
        /// A unique name that identifies the client to GitHub.  This should be the name of the
        /// product, GitHub organization, or the GitHub username (in that order of preference) that
        /// is using the Octokit framework.
        ///</summary>
        public static readonly string GitHubIdentity = Assembly
            .GetEntryAssembly()
            .GetCustomAttribute<AssemblyProductAttribute>()
            .Product;
        #endregion // Constants

        #region Private data
        private BackgroundType backgroundType;
        private SearchResult searchResult;
        private Searcher activeSearcher;
        private Searcher searcher;
        private GitHubClient client;
        private User currentUser;
        private object fullDetail;
        private int maximumCount = 1000;
        private int previousCount;
        private bool isExitPending;
        #endregion // Private data

#pragma warning disable IDE1006 
        #region Events
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e) =>
            SaveSettings();

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!mainBackgroundWorker.IsBusy)
                return;

            if (!isExitPending && mainBackgroundWorker.CancellationPending)
                mainBackgroundWorker.CancelAsync();

            isExitPending = true;
            e.Cancel = true;
            return;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LoadSettings();
            BeginInvoke((MethodInvoker)ShowLoginForm);
        }

        private void dataGridSelectColumnsMenuItem_Click(object sender, EventArgs e) =>
            ShowColumnForm();

        private void detailGetMenuItem_Click(object sender, EventArgs e) =>
            GetFullDetail();

        private void editFindCodeMenuItem_Click(object sender, EventArgs e) =>
            Search<SearchCodeBroker>();

        private void editFindIssueMenuItem_Click(object sender, EventArgs e) =>
            Search<SearchIssuesBroker>();

        private void editFindLabelMenuItem_Click(object sender, EventArgs e) =>
            Search<SearchLabelsBroker>();

        private void editFindRepositoryMenuItem_Click(object sender, EventArgs e) =>
            Search<SearchRepositoriesBroker>();

        private void editFindUserMenuItem_Click(object sender, EventArgs e) =>
            Search<SearchUsersBroker>();

        private void editSelectColumnsMenuItem_Click(object sender, EventArgs e) =>
            ShowColumnForm();

        private void helpAboutMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new MainAboutBox())
                dialog.ShowDialog(this);
        }

        private void mainBackgroundWorker_DoWork(object sender, DoWorkEventArgs e) =>
            DoWork(e);

        private void mainBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
        }

        private void mainBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) =>
            CompleteWork(e);

        private void mainDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e) =>
            FormatNestedProperty(sender as DataGridView, e);

        private void mainDataGridView_DoubleClick(object sender, EventArgs e) =>
            mainTabControl.SelectedTab = detailTabPage;

        private void mainDataGridView_SelectionChanged(object sender, EventArgs e) =>
            UpdateDetail();

        private void progressTimer_Tick(object sender, EventArgs e) =>
            UpdateProgress();

        private void viewDetailMenuItem_Click(object sender, EventArgs e) =>
            mainTabControl.SelectedTab = detailTabPage;

        private void viewFullDetailMenuItem_Click(object sender, EventArgs e) =>
            GetFullDetail();
        #endregion // Events
#pragma warning restore IDE1006 

        #region Private methods
        private void AddColumns()
        {
            mainDataGridView.DataSource = null;
            mainDataGridView.Columns.Clear();

            DataGridViewColumnCollection columns = mainDataGridView.Columns;

            Type type = searchResult.ItemType;

            foreach (string name in searcher.Columns.Selected)
            {
                PropertyInfo property = ColumnSet.GetProperty(type, name);
                Type propertyType = property.PropertyType;

                columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = name,
                    HeaderText = name,
                    Name = name,
                    ValueType = property.PropertyType,
                    Tag = property
                });
            }

            mainDataGridView.DataSource = searchResult.DataSource;

            foreach (DataGridViewColumn column in columns)
            {
                int width = column.Width;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                column.Width = width;
            }

            mainTabControl.SelectedTab = listTabPage;
            EnableUI(true);
        }

        private void BeginWork(BackgroundType type, object argument)
        {
            activeSearcher = null;
            fullDetail = null;

            switch (type)
            {
                case BackgroundType.Search:
                    if (argument is Searcher searcher)
                    {
                        mainStatusLabel.Text = $"Searching for {searcher.Type}...";
                        mainProgressBar.Visible = true;
                        mainProgressBar.Value = 0;
                        activeSearcher = searcher;
                    }
                    break;

                case BackgroundType.Detail:
                    mainStatusLabel.Text = $"Getting full detail...";
                    break;

                default:
                    return;
            }

            EnableUI(false);
            progressTimer.Start();
            backgroundType = type;
            mainBackgroundWorker.RunWorkerAsync(argument);
        }
        
        private void CompleteDetail()
        {
            if (fullDetail == null)
            {
                EndWork("Operation failed.");
                return;
            }

            detailPropertyGrid.SelectedObject = new TypeBroker(fullDetail);
            viewFullDetailMenuItem.Enabled = false;
            detailGetMenuItem.Enabled = false;
            fullDetail = null;
            EndWork("Full detail obtained.");
        }

        private void CompleteSearch()
        {
            if (searchResult == null)
            {
                mainDataGridView.DataSource = null;
                EndWork("Operation failed.");
                return;
            }

            AddColumns();

            string incompleteText = searchResult.IncompleteResults ?
                " (incomplete Results)" : string.Empty;

            EndWork($"{searchResult.DataSource.Count} of {searchResult.TotalCount} matches " +
                $"loaded{incompleteText}.");
        }

        private void CompleteWork(RunWorkerCompletedEventArgs e)
        {
            BackgroundType type = backgroundType;
            backgroundType = BackgroundType.None;

            progressTimer.Stop();
            mainProgressBar.Visible = false;
            mainProgressBar.Value = 0;
            activeSearcher = null;
            previousCount = 0;
            EnableUI(true);

            if (isExitPending)
            {
                EndWork("Operation cancelled, closing application...");
                Close();
                return;
            }

            if (e.Error != null)
            {
                MessageBox.Show(this, e.Error.Message, "Warning", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                EndWork($"Error: {e.Error.Message}");
                return;
            }

            if (e.Cancelled)
            {
                EndWork("Operation cancelled.");
                return;
            }

            switch (type)
            {
                case BackgroundType.Search:
                    CompleteSearch();
                    break;

                case BackgroundType.Detail:
                    CompleteDetail();
                    break;

                case BackgroundType.None:
                    EndWork("Operation failed.");
                    break;
            }
        }

        private void CreateClient(Credentials credentials)
        {
            try
            {
                client = new GitHubClient(new ProductHeaderValue(GitHubIdentity));
                if (credentials == null)
                {
                    currentUser = null;
                    return;
                }

                client.Credentials = credentials;
                currentUser = client.User
                    .Current()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                client = null;
                MessageBox.Show(this, ex.Message, "Authentication Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Searcher CreateSearcher(ISearchBroker broker)
        {
            try
            {
                return broker.CreateSearcher(client, maximumCount);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Invalid Search", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return null;
            }
        }

        private void DoWork(DoWorkEventArgs e)
        {
            switch (backgroundType)
            {
                case BackgroundType.Search:
                    if (e.Argument is Searcher searcher)
                        Search(searcher);
                    break;

                case BackgroundType.Detail:
                    fullDetail = e.Argument == null ?
                        null : this.searcher.GetDetail(e.Argument);
                    break;

                default:
                    break;
            }

            e.Cancel = mainBackgroundWorker.CancellationPending;
        }

        private void EnableDetailUi(bool isEnabled)
        {
            isEnabled = isEnabled && !mainBackgroundWorker.IsBusy;

            DataGridViewSelectedRowCollection rows = mainDataGridView.SelectedRows;
            object item = rows.Count > 0 ? rows[0].DataBoundItem : null;

            object propertyItem = detailPropertyGrid.SelectedObject is TypeBroker broker ?
                broker.Actual : item;

            bool hasDetail = searcher?.CanGetDetail ?? false;

            bool lacksDetail = isEnabled && hasDetail && rows
                .OfType<DataGridViewRow>()
                .Any(row => row.DataBoundItem == propertyItem);

            viewFullDetailMenuItem.Enabled = lacksDetail;
            detailGetMenuItem.Enabled = lacksDetail;
            viewDetailMenuItem.Enabled = isEnabled;
        }

        private void EnableUI(bool isEnabled)
        {
            isEnabled = isEnabled && !mainBackgroundWorker.IsBusy;

            editSelectColumnsMenuItem.Enabled = isEnabled && mainDataGridView.DataSource != null;
            editFindCodeMenuItem.Enabled = isEnabled;
            editFindLabelMenuItem.Enabled = isEnabled;
            editFindIssueMenuItem.Enabled = isEnabled;
            editFindRepositoryMenuItem.Enabled = isEnabled;
            editFindUserMenuItem.Enabled = isEnabled;
            mainTabControl.Enabled = isEnabled;
            EnableDetailUi(isEnabled);
        }

        private void EndWork(string statusText) =>
            mainStatusLabel.Text = statusText;

        private void FormatNestedProperty(DataGridView grid, DataGridViewCellFormattingEventArgs e)
        {
            if (grid == null || e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count ||
                e.ColumnIndex < 0 || e.ColumnIndex >= grid.Columns.Count)
                return;

            DataGridViewColumn column = grid.Columns[e.ColumnIndex];
            DataGridViewRow row = grid.Rows[e.RowIndex];
            object item = row.DataBoundItem;

            if (item == null || !column.DataPropertyName.Contains('.'))
                return;

            if (ColumnSet.TryGetNestedPropertyValue(column.DataPropertyName, item, out object value))
                e.Value = value;
        }

        private void GetFullDetail()
        {
            if (searcher != null && (detailPropertyGrid.SelectedObject is TypeBroker broker))
                BeginWork(BackgroundType.Detail, broker.Actual);
        }

        private bool LoadColumnSettings()
        {
            bool isChanged = false;

            CodeSearcher.SavedColumns = ParseColumns(typeof(SearchCode),
                Settings.Default.ColumnsCode, CodeSearcher.DefaultColumns, ref isChanged);
            IssueSearcher.SavedColumns = ParseColumns(typeof(Issue),
                Settings.Default.ColumnsIssue, IssueSearcher.DefaultColumns, ref isChanged);
            LabelSearcher.SavedColumns = ParseColumns(typeof(Octokit.Label),
                Settings.Default.ColumnsLabel, LabelSearcher.DefaultColumns, ref isChanged);
            RepositorySearcher.SavedColumns = ParseColumns(typeof(Repository),
                Settings.Default.ColumnsRepository, RepositorySearcher.DefaultColumns, ref isChanged);
            UserSearcher.SavedColumns = ParseColumns(typeof(User),
                Settings.Default.ColumnsUser, UserSearcher.DefaultColumns, ref isChanged);

            return isChanged;
        }

        private void LoadSettings()
        {
            bool isChanged = UpgradeSettings();

            if (LoadWindowSettings())
            {
                SaveWindowSettings();
                isChanged = true;
            }

            if (LoadColumnSettings())
            {
                SaveColumnSettings();
                isChanged = true;
            }

            if (isChanged)
                Settings.Default.Save();
        }

        private bool LoadWindowSettings()
        {
            if (Settings.Default.WindowSize == Size.Empty)
                return true;

            Size = Settings.Default.WindowSize;
            Location = Settings.Default.WindowLocation;
            WindowState = Settings.Default.WindowState;
            return false;
        }

        private ColumnSet ParseColumns(Type type, string columns, ColumnSet defaultColumns,
            ref bool isChanged)
        {
            try
            {
                List<string> selectedColumns = columns
                    .Split(',')
                    .Where(column => !string.IsNullOrWhiteSpace(column))
                    .ToList();

                if (selectedColumns.Count > 0)
                {
                    ColumnSet result = new ColumnSet(type, Searcher.Depth, selectedColumns);
                    isChanged |= columns != result.ToString();
                    return result;
                }
            }
            catch
            {
            }

            isChanged = true;
            return defaultColumns;
        }

        private void SaveColumnSettings()
        {
            Settings.Default.ColumnsCode = CodeSearcher.SavedColumns.ToString();
            Settings.Default.ColumnsIssue = IssueSearcher.SavedColumns.ToString();
            Settings.Default.ColumnsLabel = LabelSearcher.SavedColumns.ToString();
            Settings.Default.ColumnsRepository = RepositorySearcher.SavedColumns.ToString();
            Settings.Default.ColumnsUser = UserSearcher.SavedColumns.ToString();
        }

        private void SaveSettings()
        {
            SaveWindowSettings();
            SaveColumnSettings();
            Settings.Default.Save();
        }

        private void SaveWindowSettings()
        {
            if (WindowState == FormWindowState.Normal)
            {
                Settings.Default.WindowLocation = Location;
                Settings.Default.WindowSize = Size;
            }

            if (WindowState != FormWindowState.Minimized)
                Settings.Default.WindowState = WindowState;
        }

        private void Search<TBroker>()
            where TBroker : ISearchBroker, new()
        {
            var broker = new TBroker();
            using (var dialog = new SearchCriteriaForm { SelectedObject = broker })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    BeginWork(BackgroundType.Search, CreateSearcher(broker));
        }

        private void Search(Searcher searcher)
        {
            searchResult = searcher.Search();
            this.searcher = searcher;
        }

        private void ShowColumnForm()
        {
            using (var dialog = new ColumnForm { InitialColumns = searcher.Columns })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    searcher.Columns = dialog.Columns;
                    AddColumns();
                    SaveColumnSettings();
                    Settings.Default.Save();
                }
            }
        }

        private void ShowLoginForm()
        {
            while (client == null)
                using (var dialog = new LoginForm())
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        CreateClient(dialog.Credentials);
                      //  GetIssues();
                        GetIssuesLabels();
                    }
                    else
                    {
                        Close();
                        return;
                    }

            EnableUI(true);
        }

        private void UpdateDetail()
        {
            DataGridViewSelectedRowCollection rows = mainDataGridView.SelectedRows;
            object item = rows.Count > 0 ? rows[0].DataBoundItem : null;

            detailPropertyGrid.SelectedObject = item == null ?
                null : new TypeBroker(item);

            EnableDetailUi(true);
        }

        private void UpdateProgress()
        {
            if (!mainBackgroundWorker.IsBusy || activeSearcher == null)
                return;

            string type = activeSearcher.Type;

            int? totalCount = activeSearcher.TotalCount;
            if (!totalCount.HasValue)
                return;

            int count = activeSearcher.Count ?? 0;
            if (count == previousCount)
                return;

            int percent = totalCount.Value == 0 ? 0 : count * 100 / totalCount.Value;
            if (mainProgressBar.Value != percent)
                mainProgressBar.Value = percent;

            mainStatusLabel.Text = $"Searching for {type} ({count} of {totalCount.Value} results)...";
            previousCount = count;
        }

        private bool UpgradeSettings()
        {
            if (Settings.Default.IsUpgraded)
                return false;

            Settings.Default.Upgrade();
            Settings.Default.IsUpgraded = true;
            return true;
        }

        private async void GetIssues()
        {
            string owner = "dotnet";
            string name = "roslyn";

            try
            {
                string issuseIDs = "997, 992, 9861, 9850, 9817, 9815, 9759, 975, 961," +
                    "960, 9581, 9542, 946, 9448, 9447, 9446, 9360, 9359, 9248, 917, 916," +
                    "914, 9047, 904, 901, 8948, 89, 8889, 8836, 882, 880, 878, 8712, 870, " +
                    "8421, 842, 839, 8339, 8308, 8307, 826, 820, 819, 818, 8178, 8119, 8018, " +
                    "787, 7665, 7659, 750, 75, 7477, 7469, 7404, 7398, 7366, 7330, 7317, 7312," +
                    " 7262, 726, 7250, 7217, 7216, 7212, 7211, 7210, 7186, 7179, 7016, 699, 6966," +
                    " 6927, 6875, 663, 65, 6451, 6266, 614, 6, 597, 5625, 557, 5566, 552, 5519, " +
                    "5515, 5498, 5319, 5314, 520, 519, 513, 511, 5029, 5002, 4958, 485, 4552, 4545," +
                    " 4498, 4363, 4250, 4216, 4201, 4154, 4068, 4037, 3923, 38874, 3840, 38348, 383, " +
                    "38232, 38226, 38196, 38177, 38135, 38129, 38105, 381, 38037, 38014, 38010, 37915, " +
                    "37818, 37789, 37772, 37755, 37713, 37711, 37691, 37624, 37572, 37527, 37493, 37467, " +
                    "37450, 37427, 37344, 37231, 37095, 37082, 37026, 36982, 36979, 36940, 36938, 36894, " +
                    "36678, 36640, 36513, 36496, 36443, 36416, 36381, 36371, 36331, 36293, 36187, 36040, " +
                    "36029, 35962, 35780, 35764, 35709, 35664, 35584, 35463, 35411, 35301, 3527, 35120, " +
                    "35067, 35014, 35011, 34988, 34911, 34905, 34657, 345, 34316, 34292, 34266, 3426, " +
                    "34237, 34232, 34219, 34174, 34101, 34021, 33899, 33875, 33870, 33783, 33745, 33744, " +
                    "33697, 33682, 33675, 33666, 33626, 33603, 33588, 33555, 3353, 33494, 3347, 33407, " +
                    "33401, 3333, 33300, 33279, 33208, 33196, 331, 33054, 3301, 3298, 3295, 32940, 3294, " +
                    "329, 32889, 3286, 32818, 32808, 32806, 32774, 32771, 32449, 32444, 3242, 324, 3233, " +
                    "32247, 3192, 31889, 31851, 3181, 31787, 31685, 3168, 31634, 3158, 3147, 31433, 3138, " +
                    "3131, 3130, 31129, 31082, 310, 30889, 308, 30796, 30661, 306, 30596, 30587, 30566," +
                    " 30561, 30543, 30439, 30209, 30022, 3000, 3, 2996, 2977, 29655, 29591, 29569, 29517, " +
                    "29481, 29457, 2939, 29371, 29221, 29145, 2912, 2893, 28862, 288, 28667, 28634, 28382, " +
                    "2833, 28313, 28310, 28300, 28252, 28250, 2825, 28238, 28217, 28216, 28206, 28181, 28118, " +
                    "28117, 28087, 28070, 28060, 28034, 2803, 27972, 27969, 27945, 27944, 27924, 27883, 27882, " +
                    "27874, 27831, 27809, 27803, 27802, 27772, 27763, 27758, 2773, 27720, 27652, 27537, 27522," +
                    " 27484, 27424, 27411, 2740, 27371, 2736, 27357, 2733, 27321, 2732, 27279, 27220, " +
                    "27218, 27060, 27049, 27045, 27030, 26980, 26978, 26956, 2692, 26918, 26915, 26910," +
                    " 26896, 26873, 2682, 26815, 26770, 26753, 26743, 26721, 26720, 26688, 26629, 26623, " +
                    "26613, 26612, 26585, 26584, 26516, 26481, 26467, 26457, 26441, 26425, 26418, 26394, " +
                    "26390, 26387, 26368, 26361, 26345, 26344, 2631, 26287, 26274, 26248, 26195, 26154, " +
                    "26130, 26088, 26028, 26019, 26001, 26000, 260, 25999, 25903, 25893, 25829, 25813, " +
                    "2567, 25654, 256, 25529, 25432, 25399, 25211, 25209, 25131, 25086, 25070, 25043," +
                    " 25038, 25035, 25028, 24975, 24909, 2479, 24776, 24761, 24603, 24547, 24522, 24489, " +
                    "24464, 24351, 24265, 24239, 24137, 2412, 24112, 24108, 2410, 24072, 24049, 24023," +
                    " 23957, 23930, 23929, 23905, 239, 23883, 23833, 23796, 23728, 23691, 23655, 23629, " +
                    "23627, 23584, 23525, 23508, 23499, 23414, 23252, 23231, 23100, 23035, 23031, 23020, " +
                    "22994, 22856, 22830, 22779, 22768, 22717, 22645, 22641, 22640, 22619, 22614, " +
                    "22565, 22564, 22553, 22504, 22480, 22472, 22458, 22455, 22454, 22450, 22424, " +
                    "22414, 22385, 22335, 22307, 22285, 22242, 22241, 22240, 22239, 22238, 22223, " +
                    "22206, 22039, 21945, 21935, 21916, 2187, 21771, 21693, 21692, 21688, 21687, 21667, 21665, 21586, 21450, 21371, 21317, 21258, 2116, 21136, 20900, 209, 20720, 20693, 20600, 206, 20587, 20583, 20578, 20494, 20452, 20403, 20395, 20378, 20337, 203, 20210, 2019, 2017, 2015, 2012, 20103, 19868, 19845, 198, 19737, 19734, 19731, 19670, 19573, 19413, 19394, 19310, 19281, 19273, 19151, 19023, 19, 1898, 18965, 18944, 18933, 18922, 18920, 18905, 18859, 18811, 18780, 18756, 18750, 18727, 18662, 18633, 18619, 18579, 18521, 18477, 18407, 184, 18348, 18265, 18263, 18257, 18228, 18206, 18188, 18092, 17993, 17971, 17921, 17916, 17899, 17827, 17814, 17787, 17707, 17692, 17683, 17674, 17668, 17615, 1759, 1756, 17549, 17458, 17380, 17375, 17294, 17281, 17267, 17266, 17248, 17237, 17202, 17198, 17170, 17156, 17138, 17101, 17090, 17089, 17067, 17053, 17050, 17044, 16968, 16962, 16939, 16935, 16928, 16913, 16876, 16834, 16829, 16828, 16801, 16789, 16757, 16753, 16751, 16748, 16740, 16706, 16682, 16680, 16671, 16666, 16629, 16605, 16593, 16592, 16569, 16559, 16525, 16517, 16513, 16478, 16467, 16443, 16374, 16315, 16306, 16296, 16279, 16224, 16222, 16177, 16171, 16167, 16150, 16129, 16122, 16079, 16066, 16029, 15987, 15975, 15934, 15917, 15886, 15885, 15868, 15839, 15734, 15732, 15694, 15673, 15646, 15641, 15640, 15636, 15555, 15536, 15399, 15235, 15156, 15098, 15017, 14934, 14895, 14888, 14829, 14825, 14822, 14803, 14802, 14799, 14790, 14789, 14785, 14761, 14748, 14742, 14740, 14730, 14729, 14727, 14721, 14717, 14714, 14707, 14703, 14702, 14696, 14689, 14678, 14671, 14636, 14625, 14585, 14566, 14565, 14550, 14530, 14488, 14473, 14453, 14416, 1440, 14384, 14365, 14320, 14318, 14296, 14255, 14252, 14222, 14221, 14181, 14174, 14172, 14162, 14152, 14110, 14105, 14017, 14010, 13971, 1395, 13933, 13926, 13925, 13912, 13901, 13900, 13884, 13883, 13870, 13865, 13814, 13813, 13807, 13804, 13798, 13797, 1379, 13770, 13759, 13747, 13746, 13723, 13685, 13520, 13247, 13206, 13200, 13047, 13020, 12991, 12941, 12940, 12900, 12883, 12838, 12805, 12703, 12573, 12572, 1256, 1250, 12467, 1240, 12378, 12372, 1233, 12329, 1229, 12195, 12175, 12120, 12113, 12052, 1202, 12016, 11986, 11807, 11782, 11718, 11708, 11707, 1170, 11530, 1153, 11497, 11358, 1115, 11074, 11053, " +
                    "10929, 10920, 10835, 10696, 10643, 10620, 10604, 10529, 10525," +
                    " 10492, 10491, 10466, 10465, 104, 1037, 10306, 100";
                List<string> issueslists = new List<string>();
                issueslists = issuseIDs.ToString().Split(',').ToList();

                #region Issues 

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load("C:\\PhD\\Workbrench\\RoslynIssuesLabels.xml");
                XmlNode rootNode = xmlDoc["Issues"];

                foreach (var item in issueslists)
                {
                    var task = GetIssue(owner, name, Convert.ToInt32(item));
                    if (!task.IsCompleted)
                    {
                        var response = await task.ConfigureAwait(false);
                        Issue issue = response as Issue;

                        //if (issue != null)
                        //{
                        //    XmlNode IssueNode = xmlDoc.CreateElement("Issue");
                        //    XmlElement elemID = xmlDoc.CreateElement("IssueID");
                        //    elemID.InnerText = issue.Number.ToString();
                        //    IssueNode.AppendChild(elemID);

                        //    XmlElement elemRepoID = xmlDoc.CreateElement("RepoID");
                        //    elemRepoID.InnerText = 1.ToString();
                        //    IssueNode.AppendChild(elemRepoID);

                        //    XmlElement elemTitle = xmlDoc.CreateElement("Title");
                        //    elemTitle.InnerText = issue.Title?.ToString();
                        //    IssueNode.AppendChild(elemTitle);

                        //    XmlElement elemDescription = xmlDoc.CreateElement("Description");
                        //    elemDescription.InnerText = issue.Body?.ToString();
                        //    IssueNode.AppendChild(elemDescription);

                        //    XmlElement elemOpenedAt = xmlDoc.CreateElement("CreatedDate");
                        //    elemOpenedAt.InnerText = issue.CreatedAt.ToString("dd/MM/yyyy");
                        //    IssueNode.AppendChild(elemOpenedAt);

                        //    XmlElement elemClosedAt = xmlDoc.CreateElement("ClosedDate");
                        //    elemClosedAt.InnerText = issue.ClosedAt?.ToString("dd/MM/yyyy");
                        //    IssueNode.AppendChild(elemClosedAt);

                        //    rootNode.AppendChild(IssueNode);
                        //}

                    }
                }


                xmlDoc.Save("C:\\PhD\\Workbrench\\RoslynIssuesLabels.xml");
                MessageBox.Show("Done");
                #endregion Issues


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async void GetIssuesLabels()
        {
            string owner = "dotnet";
            string name = "roslyn";

            try
            {
                string issuseIDs = "997, 992, 9861, 9850, 9817, 9815, 9759, 975, 961," +
                    "960, 9581, 9542, 946, 9448, 9447, 9446, 9360, 9359, 9248, 917, 916," +
                    "914, 9047, 904, 901, 8948, 89, 8889, 8836, 882, 880, 878, 8712, 870, " +
                    "8421, 842, 839, 8339, 8308, 8307, 826, 820, 819, 818, 8178, 8119, 8018, " +
                    "787, 7665, 7659, 750, 75, 7477, 7469, 7404, 7398, 7366, 7330, 7317, 7312," +
                    " 7262, 726, 7250, 7217, 7216, 7212, 7211, 7210, 7186, 7179, 7016, 699, 6966," +
                    " 6927, 6875, 663, 65, 6451, 6266, 614, 6, 597, 5625, 557, 5566, 552, 5519, " +
                    "5515, 5498, 5319, 5314, 520, 519, 513, 511, 5029, 5002, 4958, 485, 4552, 4545," +
                    " 4498, 4363, 4250, 4216, 4201, 4154, 4068, 4037, 3923, 38874, 3840, 38348, 383, " +
                    "38232, 38226, 38196, 38177, 38135, 38129, 38105, 381, 38037, 38014, 38010, 37915, " +
                    "37818, 37789, 37772, 37755, 37713, 37711, 37691, 37624, 37572, 37527, 37493, 37467, " +
                    "37450, 37427, 37344, 37231, 37095, 37082, 37026, 36982, 36979, 36940, 36938, 36894, " +
                    "36678, 36640, 36513, 36496, 36443, 36416, 36381, 36371, 36331, 36293, 36187, 36040, " +
                    "36029, 35962, 35780, 35764, 35709, 35664, 35584, 35463, 35411, 35301, 3527, 35120, " +
                    "35067, 35014, 35011, 34988, 34911, 34905, 34657, 345, 34316, 34292, 34266, 3426, " +
                    "34237, 34232, 34219, 34174, 34101, 34021, 33899, 33875, 33870, 33783, 33745, 33744, " +
                    "33697, 33682, 33675, 33666, 33626, 33603, 33588, 33555, 3353, 33494, 3347, 33407, " +
                    "33401, 3333, 33300, 33279, 33208, 33196, 331, 33054, 3301, 3298, 3295, 32940, 3294, " +
                    "329, 32889, 3286, 32818, 32808, 32806, 32774, 32771, 32449, 32444, 3242, 324, 3233, " +
                    "32247, 3192, 31889, 31851, 3181, 31787, 31685, 3168, 31634, 3158, 3147, 31433, 3138, " +
                    "3131, 3130, 31129, 31082, 310, 30889, 308, 30796, 30661, 306, 30596, 30587, 30566," +
                    " 30561, 30543, 30439, 30209, 30022, 3000, 3, 2996, 2977, 29655, 29591, 29569, 29517, " +
                    "29481, 29457, 2939, 29371, 29221, 29145, 2912, 2893, 28862, 288, 28667, 28634, 28382, " +
                    "2833, 28313, 28310, 28300, 28252, 28250, 2825, 28238, 28217, 28216, 28206, 28181, 28118, " +
                    "28117, 28087, 28070, 28060, 28034, 2803, 27972, 27969, 27945, 27944, 27924, 27883, 27882, " +
                    "27874, 27831, 27809, 27803, 27802, 27772, 27763, 27758, 2773, 27720, 27652, 27537, 27522," +
                    " 27484, 27424, 27411, 2740, 27371, 2736, 27357, 2733, 27321, 2732, 27279, 27220, " +
                    "27218, 27060, 27049, 27045, 27030, 26980, 26978, 26956, 2692, 26918, 26915, 26910," +
                    " 26896, 26873, 2682, 26815, 26770, 26753, 26743, 26721, 26720, 26688, 26629, 26623, " +
                    "26613, 26612, 26585, 26584, 26516, 26481, 26467, 26457, 26441, 26425, 26418, 26394, " +
                    "26390, 26387, 26368, 26361, 26345, 26344, 2631, 26287, 26274, 26248, 26195, 26154, " +
                    "26130, 26088, 26028, 26019, 26001, 26000, 260, 25999, 25903, 25893, 25829, 25813, " +
                    "2567, 25654, 256, 25529, 25432, 25399, 25211, 25209, 25131, 25086, 25070, 25043," +
                    " 25038, 25035, 25028, 24975, 24909, 2479, 24776, 24761, 24603, 24547, 24522, 24489, " +
                    "24464, 24351, 24265, 24239, 24137, 2412, 24112, 24108, 2410, 24072, 24049, 24023," +
                    " 23957, 23930, 23929, 23905, 239, 23883, 23833, 23796, 23728, 23691, 23655, 23629, " +
                    "23627, 23584, 23525, 23508, 23499, 23414, 23252, 23231, 23100, 23035, 23031, 23020, " +
                    "22994, 22856, 22830, 22779, 22768, 22717, 22645, 22641, 22640, 22619, 22614, " +
                    "22565, 22564, 22553, 22504, 22480, 22472, 22458, 22455, 22454, 22450, 22424, " +
                    "22414, 22385, 22335, 22307, 22285, 22242, 22241, 22240, 22239, 22238, 22223, " +
                    "22206, 22039, 21945, 21935, 21916, 2187, 21771, 21693, 21692, 21688, 21687, " +
                    "21667, 21665, 21586, 21450, 21371, 21317, 21258, 2116, 21136, 20900, 209, " +
                    "20720, 20693, 20600, 206, 20587, 20583, 20578, 20494, 20452, 20403, 20395," +
                    " 20378, 20337, 203, 20210, 2019, 2017, 2015, 2012, 20103, 19868, 19845," +
                    " 198, 19737, 19734, 19731, 19670, 19573, 19413, 19394, 19310, 19281, 19273, " +
                    "19151, 19023, 19, 1898, 18965, 18944, 18933, 18922, 18920, 18905, 18859, 18811, " +
                    "18780, 18756, 18750, 18727, 18662, 18633, 18619, 18579, 18521, 18477, 18407, 184, 18348, 18265, 18263, 18257, 18228, 18206, 18188, 18092, 17993, 17971, 17921, 17916, 17899, 17827, 17814, 17787, 17707, 17692, 17683, 17674, 17668, 17615, 1759, 1756, 17549, 17458, 17380, 17375, 17294, 17281, 17267, 17266, 17248, 17237, 17202, 17198, 17170, 17156, 17138, 17101, 17090, 17089, 17067, 17053, 17050, 17044, 16968, 16962, 16939, 16935, 16928, 16913, 16876, 16834, 16829, 16828, 16801, 16789, 16757, 16753, 16751, 16748, 16740, 16706, 16682, 16680, 16671, 16666, 16629, 16605, 16593, 16592, 16569, 16559, 16525, 16517, 16513, 16478, 16467, 16443, 16374, 16315, 16306, 16296, 16279, 16224, 16222, 16177, 16171, 16167, 16150, 16129, 16122, 16079, 16066, 16029, 15987, 15975, 15934, 15917, 15886, 15885, 15868, 15839, 15734, 15732, 15694, 15673, 15646, 15641, 15640, 15636, 15555, 15536, 15399, 15235, 15156, 15098, 15017, 14934, 14895, 14888, 14829, 14825, 14822, 14803, 14802, 14799, 14790, 14789, 14785, 14761, 14748, 14742, 14740, 14730, 14729, 14727, 14721, 14717, 14714, 14707, 14703, 14702, 14696, 14689, 14678, 14671, 14636, 14625, 14585, 14566, 14565, 14550, 14530, 14488, 14473, 14453, 14416, 1440, 14384, 14365, 14320, 14318, 14296, 14255, 14252, 14222, 14221, 14181, 14174, 14172, 14162, 14152, 14110, 14105, 14017, 14010, 13971, 1395, 13933, 13926, 13925, 13912, 13901, 13900, 13884, 13883, 13870, 13865, 13814, 13813, 13807, 13804, 13798, 13797, 1379, 13770, 13759, 13747, 13746, 13723, 13685, 13520, 13247, 13206, 13200, 13047, 13020, 12991, 12941, 12940, 12900, 12883, 12838, 12805, 12703, 12573, 12572, 1256, 1250, 12467, 1240, 12378, 12372, 1233, 12329, 1229, 12195, 12175, 12120, 12113, 12052, 1202, 12016, 11986, 11807, 11782, 11718, 11708, 11707, 1170, 11530, 1153, 11497, 11358, 1115, 11074, 11053, " +
                    "10929, 10920, 10835, 10696, 10643, 10620, 10604, 10529, 10525," +
                    " 10492, 10491, 10466, 10465, 104, 1037, 10306, 100";
                List<string> issueslists = new List<string>();
                issueslists = issuseIDs.ToString().Split(',').ToList();

                #region Issues 

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load("C:\\PhD\\Workbrench\\Roslyn\\RoslynIssuesLabels.xml");
                XmlNode rootNode = xmlDoc["IssueLabels"];

                foreach (var item in issueslists)
                {
                    var task = GetIssue(owner, name, Convert.ToInt32(item));
                    if (!task.IsCompleted)
                    {
                        var response = await task.ConfigureAwait(false);
                        Issue issue = response as Issue;

                        if (issue != null)
                        {
                            if(issue.Labels != null)
                            {
                                foreach(var label in issue.Labels)
                                {
                                    XmlNode IssueNode = xmlDoc.CreateElement("IssueLabel");
                                    XmlElement elemLabel = xmlDoc.CreateElement("Label");
                                    elemLabel.InnerText = label.Name.ToString();
                                    IssueNode.AppendChild(elemLabel);

                                    XmlElement elemIssueLabelID = xmlDoc.CreateElement("IssueLabelID");
                                    elemIssueLabelID.InnerText = issue.Number.ToString();
                                    IssueNode.AppendChild(elemIssueLabelID);

                                    rootNode.AppendChild(IssueNode);
                                }
                            }                           
                        }
                    }
                }

                xmlDoc.Save("C:\\PhD\\Workbrench\\Roslyn\\RoslynIssuesLabels.xml");
                MessageBox.Show("Done");
                #endregion Issues


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public async Task<object> GetIssue(string owner, string name, int item)
        {
            try
            {
                return await client.Issue.Get(owner, name, item);
            }
            catch (Exception ex)
            {
                return null;
            }

        }
        #endregion // Private methods

        #region Private enums
        private enum BackgroundType
        {
            None,
            Search,
            Detail
        }
        #endregion // Private enums
    }
}

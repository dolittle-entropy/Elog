using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;

namespace OutputWriting
{
    public class LiveKeyAction<T>
    {
        public LiveKeyAction(char key, string description, Action<T> action)
        {
            Key = key;
            Description = description;
            Action = action;
        }

        public char Key { get; set; }
        public string Description { get; set; }
        public Action<T> Action { get; set; }
    }

    public class LiveDataTable<T>
    {
        private readonly int _pageSize = 20;
        private int _currentPage = 0;
        private int _pageCount = 0;
        private int _dataIndex = 0;

        private List<T> _sourceData;
        private string _tableHeader;
        private List<string> _tableColumns;
        private Table _dataTable;
        Func<T, IEnumerable<string>> _picker;
        private Action<T> _selectionAction;
        private T? _selectedItem;
        private string? _enterInstruction;
        private Func<T, string> _idValuePicker;
        private List<LiveKeyAction<T>> _keyActions;
        private readonly Table _masterTable;

        public LiveDataTable()
        {
            _masterTable = new Table().Border(TableBorder.None);
            _masterTable.AddColumn("");

        }

        public LiveDataTable<T> WithHeader(string message)
        {
            _tableHeader = ColorAs.Info(message);
            return this;
        }

        /// <summary>
        /// Provide instructions for what should happen when the user presses [ENTER]. 
        /// Attempt to complete the sentence, so start your message in lowercase.
        /// </summary>
        /// <param name="message">The message that indicates what happens when you press [ENTER]</param>
        /// <param name="idValuePicker">Optional idValuePicker, so you can format your message with the selected Id</param>
        /// <returns></returns>
        public LiveDataTable<T> WithEnterInstruction(string message, Func<T, string>? idValuePicker = null)
        {
            _enterInstruction = message;
            _idValuePicker = idValuePicker!;

            return this;
        }

        public LiveDataTable<T> WithoutBorders()
        {
            _masterTable.Border(TableBorder.None);
            return this;
        }

        public LiveDataTable<T> WithMultipleActions(params LiveKeyAction<T>[] actions)
        {
            _keyActions = new List<LiveKeyAction<T>>(actions);
            return this;
        }

        public LiveDataTable<T> WithColumns(params string[] columns)
        {
            _tableColumns = new List<string>();
            foreach (var column in columns)
                _tableColumns.Add($"[bold orangered1]{column}[/]");

            return this;
        }

        public LiveDataTable<T> WithSelectionAction(Action<T> selectionAction)
        {
            _selectionAction = selectionAction;
            return this;
        }

        public LiveDataTable<T> WithDataPicker(Func<T, List<string>> picker)
        {
            _picker = picker;
            return this;
        }

        public LiveDataTable<T> WithDataSource(IEnumerable<T> sourceData)
        {
            _sourceData = sourceData!.ToList();
            return this;
        }

        public void Start()
        {
            if (_sourceData.Count == 0)
                return;

            _pageCount = _sourceData.Count / _pageSize;
            _selectedItem = _sourceData[0];

            AnsiConsole.Live(_masterTable)
                .Start(ctx =>
                {
                    _masterTable.AddRow(_tableHeader);
                    BuildDataTable(ctx);
                    PerformPaging(ctx);
                });

            if (_selectedItem != null)
                _selectionAction(_selectedItem);
        }

        void PerformPaging(LiveDisplayContext ctx)
        {
            bool keepGoing = true;
            do
            {
                var keyAction = Console.ReadKey();

                // MultiKey Actions
                if (_keyActions != null && _keyActions.Any())
                {
                    if (_keyActions.FirstOrDefault(ka => ka.Key == keyAction.KeyChar) is LiveKeyAction<T> result)
                    {
                        if (_selectedItem != null)
                        {
                            var skip = _pageSize * _currentPage;
                            var subset = _sourceData.Skip(skip).Take(_pageSize);
                            _selectedItem = subset.ElementAt(_dataIndex); _selectionAction = result.Action;
                            keepGoing = false;
                            break;
                        }
                    }
                }

                // Next page
                if (keyAction.Key == ConsoleKey.RightArrow || keyAction.Key == ConsoleKey.PageDown)
                {
                    if (++_currentPage > _pageCount)
                        _currentPage = _pageCount;

                }

                // Previous page
                else if (keyAction.Key == ConsoleKey.LeftArrow || keyAction.Key == ConsoleKey.PageUp)
                {
                    if (--_currentPage < 0)
                        _currentPage = 0;
                }

                // Last page
                else if (keyAction.Key == ConsoleKey.End || (keyAction.Modifiers.HasFlag(ConsoleModifiers.Control) && keyAction.Key == ConsoleKey.RightArrow))
                {
                    _currentPage = _pageCount;
                }

                // First page
                else if (keyAction.Key == ConsoleKey.Home || (keyAction.Modifiers.HasFlag(ConsoleModifiers.Control) && keyAction.Key == ConsoleKey.LeftArrow))
                {
                    _currentPage = 0;
                }

                // Select index downwards
                else if (keyAction.Key == ConsoleKey.DownArrow)
                {
                    if (++_dataIndex >= _pageSize)
                        _dataIndex = _pageSize;
                }

                // Select index upwards
                else if (keyAction.Key == ConsoleKey.UpArrow)
                {
                    if (--_dataIndex < 0)
                        _dataIndex = 0;
                }

                // We selected a row
                else if (keyAction.Key == ConsoleKey.Enter)
                {
                    keepGoing = false;
                    var skip = _pageSize * _currentPage;
                    var subset = _sourceData.Skip(skip).Take(_pageSize);
                    _selectedItem = subset.ElementAt(_dataIndex);
                }

                // Clean up before exiting
                else
                {
                    _selectedItem = default;
                    keepGoing = false;
                }
                if (keepGoing)
                    BuildDataTable(ctx);

            } while (keepGoing);
        }

        void ClearDataRows(LiveDisplayContext ctx)
        {
            if (_dataTable is null)
                return;

            while (_dataTable.Rows.Count > 0)
                _dataTable.Rows.RemoveAt(0);

            ctx.Refresh();
        }

        void ClearMasterTable(LiveDisplayContext ctx)
        {
            if (_masterTable is { } && _masterTable.Rows.Count > 0)
            {
                for (int i = _masterTable.Rows.Count - 1; i > 0; i--)
                    _masterTable.Rows.RemoveAt(i);
                ctx.Refresh();
            }
        }

        void BuildDataTable(LiveDisplayContext ctx)
        {

            ClearDataRows(ctx);
            ClearMasterTable(ctx);

            _dataTable = new Table().Border(TableBorder.Rounded);
            _tableColumns.ForEach(c => _dataTable.AddColumn(c));

            AddDataRowsFromSource(ctx);

            _masterTable.AddRow(_dataTable);

            BuildTableFooter(ctx);
        }

        void AddDataRowsFromSource(LiveDisplayContext ctx)
        {
            var skipAmount = _currentPage * _pageSize;
            var subset = _sourceData.Skip(skipAmount).Take(_pageSize).ToList();
            for (int i = 0; i < subset.Count; i++)
            {
                var dataValues = _picker(subset[i]);
                if (_dataIndex == i)
                {

                    var colored = dataValues.Select(v => ColorAs.Success(v));
                    _dataTable.AddRow(colored.ToArray());
                }
                else
                {
                    _dataTable.AddRow(dataValues.ToArray());
                }
                while (_dataIndex >= subset.Count)
                    _dataIndex--;

                _selectedItem = subset[_dataIndex];
                ctx.Refresh();
            }
        }

        void BuildTableFooter(LiveDisplayContext ctx)
        {
            var realCount = 3 + _keyActions?.Count ?? 0;
            if (_masterTable.Rows.Count == realCount)
                _masterTable.RemoveRow(2 + realCount);

            var pageInfo = _pageCount > 0
                ? $"On page {_currentPage}/{_pageCount}"
                : string.Empty;

            var message = _enterInstruction ?? string.Empty;
            if (_idValuePicker != null)
                message = string.Format(_enterInstruction!, _idValuePicker(_selectedItem!));

            var finalMessage = pageInfo;

            if (pageInfo.Length > 0 && message.Length > 0)
                finalMessage = $"{pageInfo}, press [[ENTER]] to {message}";

            else if (pageInfo.Length == 0 && message.Length > 0)
                finalMessage = $"Press [[ENTER]] to {message}";

            if (!string.IsNullOrEmpty(finalMessage))
            {
                _masterTable.AddRow($"[tan italic]{finalMessage}[/]");
            }

            if (_keyActions != null && _keyActions.Count > 0)
            {
                var anotherTable = new Table();
                anotherTable.Border(TableBorder.None);
                anotherTable.HideHeaders();
                anotherTable.AddColumns("Key", "Description");
                _keyActions.ForEach(a => anotherTable.AddRow($"[tan italic][[{a.Key}]][/]", $"[tan italic]{a.Description}[/]"));

                _masterTable.AddRow($"[tan italic]Additional commands:[/]");
                _masterTable.AddRow(anotherTable);
            }
            ctx.Refresh();
        }
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TTXView.Wpf;

public partial class MainWindow : Window
{
    private const string DefaultCategory = "默认";
    private const string SymbolDragFormat = "TTXView.SymbolCode";
    private const string CategoryDragFormat = "TTXView.CategoryName";
    private readonly ConfigStore _configStore = new();
    private readonly MarketDataService _marketData = new();
    private readonly DispatcherTimer _clockTimer = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private AppConfig _config = new();
    private Dictionary<string, QuoteItem> _quotes = new();
    private string? _selectedCode;
    private Point _dragStart;
    private Point _resizeStartScreen;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private bool _isLight;
    private bool _initializing = true;

    private double AppearanceOpacity => Math.Clamp(_config.Appearance.Opacity, 0.35, 1.0);

    private Brush ShellBrush => _isLight
        ? new LinearGradientBrush(Color.FromArgb(SurfaceAlpha(188), 246, 250, 255), Color.FromArgb(SurfaceAlpha(168), 224, 235, 248), 45)
        : new LinearGradientBrush(Color.FromArgb(SurfaceAlpha(188), 20, 28, 39), Color.FromArgb(SurfaceAlpha(168), 7, 13, 22), 45);

    private Brush PanelBrush => _isLight ? ColorBrush(SurfaceAlpha(208), 250, 253, 255) : ColorBrush(SurfaceAlpha(130), 30, 39, 53);
    private Brush RowBrush => _isLight ? ColorBrush(SurfaceAlpha(198), 235, 241, 249) : ColorBrush(SurfaceAlpha(118), 42, 52, 66);
    private Brush TextBrush => _isLight ? ColorBrush(255, 16, 31, 48) : ColorBrush(255, 248, 250, 252);
    private Brush MutedBrush => _isLight ? ColorBrush(175, 52, 65, 82) : ColorBrush(170, 226, 236, 247);
    private Brush LineBrush => _isLight ? ColorBrush(70, 82, 103, 126) : ColorBrush(48, 255, 255, 255);
    private Brush SelectedBrush => ColorBrush(255, 108, 180, 255);

    public MainWindow()
    {
        InitializeComponent();
        _config = _configStore.Load();
        _isLight = _config.Appearance.Theme == "light";
        Topmost = _config.Appearance.AlwaysOnTop;
        OpacitySlider.Value = _config.Appearance.Opacity * 100;
        UpdateSearchHint();
        _initializing = false;

        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _clockTimer.Start();

        ApplyRefreshInterval();
        _refreshTimer.Tick += async (_, _) => await RefreshQuotesAsync();
        _refreshTimer.Start();

        ApplyTheme();
        Render();
        Loaded += async (_, _) =>
        {
            ApplyTheme();
            await RefreshQuotesAsync();
        };
    }

    private async Task RefreshQuotesAsync()
    {
        StatusText.Text = "刷新中...";
        var quotes = await _marketData.FetchAsync(_config.Symbols);
        _quotes = quotes.ToDictionary(quote => quote.Code, quote => quote);
        UpdateSymbolNames(quotes);
        StatusText.Text = "已刷新";
        Render();
    }

    private void UpdateSymbolNames(IEnumerable<QuoteItem> quotes)
    {
        var changed = false;
        foreach (var quote in quotes)
        {
            var name = quote.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name == quote.Code)
            {
                continue;
            }

            var symbol = _config.Symbols.FirstOrDefault(item => item.Code == quote.Code);
            if (symbol is not null && symbol.Name != name)
            {
                symbol.Name = name;
                changed = true;
            }
        }

        if (changed)
        {
            _configStore.Save(_config);
        }
    }

    private void Render()
    {
        CategoriesPanel.Children.Clear();
        foreach (var category in _config.Categories)
        {
            var symbols = _config.Symbols.Where(symbol => symbol.Category == category).ToList();
            CategoriesPanel.Children.Add(CreateCategoryPanel(category, symbols));
        }
        OpacityText.Text = $"{(int)OpacitySlider.Value}%";
    }

    private Border CreateCategoryPanel(string category, List<SymbolItem> symbols)
    {
        var panel = new StackPanel();
        var border = new Border
        {
            CornerRadius = new CornerRadius(14),
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(1),
            Background = PanelBrush,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(5, 4, 5, 4),
            Child = panel,
            Tag = category,
            AllowDrop = true,
            ContextMenu = CreateCategoryContextMenu(category)
        };
        border.Drop += Category_Drop;
        border.DragOver += (_, e) => e.Effects = DragDropEffects.Move;

        var header = new Grid { Height = 24, Margin = new Thickness(4, 0, 4, 2) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        header.Children.Add(new TextBlock
        {
            Text = category,
            Foreground = MutedBrush,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });
        var count = new TextBlock
        {
            Text = symbols.Count.ToString(CultureInfo.InvariantCulture),
            Foreground = MutedBrush,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(count, 1);
        header.Children.Add(count);

        var categoryHandle = new TextBlock
        {
            Text = "≡",
            Foreground = MutedBrush,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.SizeAll,
            ToolTip = "按住拖动分类排序",
            Tag = category
        };
        categoryHandle.PreviewMouseLeftButtonDown += CategoryHandle_PreviewMouseLeftButtonDown;
        categoryHandle.PreviewMouseLeftButtonUp += CategoryHandle_PreviewMouseLeftButtonUp;
        categoryHandle.MouseMove += CategoryHandle_MouseMove;
        Grid.SetColumn(categoryHandle, 2);
        header.Children.Add(categoryHandle);
        panel.Children.Add(header);

        if (symbols.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "拖入标的",
                Foreground = MutedBrush,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 16)
            });
            return border;
        }

        foreach (var symbol in symbols)
        {
            panel.Children.Add(CreateQuoteRow(symbol));
        }
        return border;
    }

    private ContextMenu CreateCategoryContextMenu(string category)
    {
        var menu = new ContextMenu();

        var addSymbol = new MenuItem { Header = "添加标的" };
        addSymbol.Click += AddSymbol_Click;
        menu.Items.Add(addSymbol);

        var addCategory = new MenuItem { Header = "新增分类" };
        addCategory.Click += AddCategory_Click;
        menu.Items.Add(addCategory);

        var deleteCategory = new MenuItem
        {
            Header = "删除此分类",
            Tag = category,
            IsEnabled = category != DefaultCategory
        };
        deleteCategory.Click += DeleteCategory_Click;
        menu.Items.Add(deleteCategory);

        var deleteSymbol = new MenuItem { Header = "删除选中标的" };
        deleteSymbol.Click += RemoveSelected_Click;
        menu.Items.Add(deleteSymbol);

        menu.Items.Add(new Separator());

        var refresh = new MenuItem { Header = "刷新" };
        refresh.Click += Refresh_Click;
        menu.Items.Add(refresh);

        menu.Items.Add(CreateRefreshIntervalMenu());

        return menu;
    }

    private MenuItem CreateRefreshIntervalMenu()
    {
        var menu = new MenuItem { Header = "刷新时间" };
        foreach (var seconds in new[] { 1, 3, 5, 10 })
        {
            var item = new MenuItem
            {
                Header = $"{seconds}s",
                Tag = seconds,
                IsCheckable = true,
                IsChecked = _config.RefreshSeconds == seconds
            };
            item.Click += SetRefreshSeconds_Click;
            menu.Items.Add(item);
        }
        return menu;
    }

    private Border CreateQuoteRow(SymbolItem symbol)
    {
        _quotes.TryGetValue(symbol.Code, out var quote);
        var percent = quote?.Percent;
        var priceBrush = percent switch
        {
            > 0 => ColorBrush(255, 255, 82, 82),
            < 0 => ColorBrush(255, 48, 211, 143),
            _ => TextBrush
        };
        var isSelected = symbol.Code == _selectedCode;

        var row = new Border
        {
            CornerRadius = new CornerRadius(11),
            Background = RowBrush,
            BorderBrush = isSelected ? SelectedBrush : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 5),
            Padding = new Thickness(8, 4, 8, 4),
            Tag = symbol,
            AllowDrop = true,
            Cursor = Cursors.Hand
        };

        var grid = new Grid { Height = 38 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock
        {
            Text = quote?.Name ?? symbol.Name,
            Foreground = TextBrush,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        nameStack.Children.Add(new TextBlock
        {
            Text = symbol.Code,
            Foreground = MutedBrush,
            FontSize = 11,
            Margin = new Thickness(0, -2, 0, 0)
        });
        grid.Children.Add(nameStack);

        var priceText = new TextBlock
        {
            Text = quote?.Price is null ? "--" : quote.Price.Value.ToString("#,0.00", CultureInfo.InvariantCulture),
            Foreground = priceBrush,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(priceText, 1);
        grid.Children.Add(priceText);

        var pctText = new TextBlock
        {
            Text = percent is null ? "--" : $"{(percent >= 0 ? "+" : "")}{percent.Value:0.00}%",
            Foreground = priceBrush,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(pctText, 2);
        grid.Children.Add(pctText);

        var dragHandle = new TextBlock
        {
            Text = "≡",
            Foreground = MutedBrush,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.SizeAll,
            ToolTip = "按住拖动排序",
            Tag = symbol
        };
        dragHandle.PreviewMouseLeftButtonDown += DragHandle_PreviewMouseLeftButtonDown;
        dragHandle.PreviewMouseLeftButtonUp += DragHandle_PreviewMouseLeftButtonUp;
        dragHandle.MouseMove += DragHandle_MouseMove;
        Grid.SetColumn(dragHandle, 3);
        grid.Children.Add(dragHandle);

        row.Child = grid;
        row.MouseLeftButtonDown += Row_MouseLeftButtonDown;
        row.PreviewMouseRightButtonDown += Row_PreviewMouseRightButtonDown;
        row.Drop += Row_Drop;
        row.DragOver += (_, e) => e.Effects = DragDropEffects.Move;
        return row;
    }

    private void ApplyTheme()
    {
        ShellBorder.Background = ShellBrush;
        ShellBorder.BorderBrush = LineBrush;
        RootGrid.Opacity = 1.0;
        TitleText.Foreground = TextBrush;
        StatusText.Foreground = MutedBrush;
        ClockText.Foreground = MutedBrush;
        OpacityText.Foreground = MutedBrush;
        SearchBorder.Background = RowBrush;
        SearchBorder.BorderBrush = LineBrush;
        SearchBox.Foreground = TextBrush;
        SearchBox.CaretBrush = TextBrush;
        SearchBox.SelectionBrush = SelectedBrush;
        SearchHintText.Foreground = MutedBrush;
        ResizeGlyph.Stroke = MutedBrush;
        ThemeButton.Content = _isLight ? "☀" : "☾";

        foreach (var button in FindVisualChildren<Button>(this))
        {
            button.Background = Brushes.Transparent;
            button.Foreground = TextBrush;
            button.BorderBrush = Brushes.Transparent;
            button.FontWeight = FontWeights.Bold;
        }
        ApplyTopmostButtonStyle();
        Render();
    }

    private void SaveConfig()
    {
        _config.Appearance.Theme = _isLight ? "light" : "dark";
        _config.Appearance.Opacity = Math.Clamp(OpacitySlider.Value / 100, 0.35, 1.0);
        _config.Appearance.AlwaysOnTop = Topmost;
        _configStore.Save(_config);
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("新增分类", "请输入分类名称：") { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Value) && !_config.Categories.Contains(dialog.Value))
        {
            _config.Categories.Insert(Math.Max(1, _config.Categories.Count - 3), dialog.Value);
            SaveConfig();
            Render();
        }
    }

    private async void AddSymbol_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("添加标的", "输入代码/名称：") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value))
        {
            return;
        }

        await AddSymbolAsync(dialog.Value, clearSearch: false);
    }

    private async Task AddSymbolAsync(string value, bool clearSearch)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusText.Text = "请输入股票名称或代码";
            return;
        }

        StatusText.Text = "正在搜索标的...";
        var symbol = await _marketData.ResolveSymbolAsync(text);
        if (symbol is null)
        {
            StatusText.Text = "未找到标的";
            return;
        }
        symbol.Category = DefaultCategory;

        var existing = _config.Symbols.FirstOrDefault(item => item.Code == symbol.Code);
        if (existing is not null)
        {
            _selectedCode = existing.Code;
            StatusText.Text = "标的已存在";
            if (clearSearch)
            {
                SearchBox.Clear();
            }
            Render();
            return;
        }

        if (!_config.Categories.Contains(DefaultCategory))
        {
            _config.Categories.Insert(0, DefaultCategory);
        }

        _config.Symbols.Insert(0, symbol);
        _selectedCode = symbol.Code;
        StatusText.Text = "已添加，正在刷新";
        SaveConfig();
        Render();
        if (clearSearch)
        {
            SearchBox.Clear();
        }
        await RefreshQuotesAsync();
    }

    private async void SearchAdd_Click(object sender, RoutedEventArgs e)
    {
        await AddSymbolAsync(SearchBox.Text, clearSearch: true);
    }

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await AddSymbolAsync(SearchBox.Text, clearSearch: true);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchHint();
    }

    private void UpdateSearchHint()
    {
        if (SearchHintText is null || SearchBox is null)
        {
            return;
        }

        SearchHintText.Visibility = string.IsNullOrWhiteSpace(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _ = RefreshQuotesAsync();
    }

    private void SetRefreshSeconds_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetRefreshSeconds((sender as FrameworkElement)?.Tag, out var seconds))
        {
            return;
        }

        _config.RefreshSeconds = seconds;
        ApplyRefreshInterval();
        SaveConfig();
        StatusText.Text = $"刷新时间已设为 {seconds}s";
        Render();
    }

    private static bool TryGetRefreshSeconds(object? tag, out int seconds)
    {
        seconds = 0;
        return tag switch
        {
            int value when IsSupportedRefreshSeconds(value) => SetResult(value, out seconds),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                && IsSupportedRefreshSeconds(value) => SetResult(value, out seconds),
            _ => false
        };
    }

    private static bool SetResult(int value, out int result)
    {
        result = value;
        return true;
    }

    private static bool IsSupportedRefreshSeconds(int seconds) => seconds is 1 or 3 or 5 or 10;

    private void ApplyRefreshInterval()
    {
        _config.RefreshSeconds = IsSupportedRefreshSeconds(_config.RefreshSeconds) ? _config.RefreshSeconds : 10;
        _refreshTimer.Interval = TimeSpan.FromSeconds(_config.RefreshSeconds);
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedCode))
        {
            StatusText.Text = "请先选择要删除的标的";
            return;
        }

        var code = _selectedCode;
        var removed = _config.Symbols.RemoveAll(symbol => symbol.Code == code);
        _quotes.Remove(code);
        _selectedCode = null;

        if (removed > 0)
        {
            StatusText.Text = "已删除";
            SaveConfig();
        }
        Render();
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        var category = (sender as FrameworkElement)?.Tag as string;
        if (string.IsNullOrWhiteSpace(category))
        {
            var dialog = new InputDialog("删除分类", "输入要删除的分类名称：") { Owner = this };
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value))
            {
                return;
            }
            category = dialog.Value.Trim();
        }

        DeleteCategory(category);
    }

    private void DeleteCategory(string category)
    {
        if (category == DefaultCategory)
        {
            StatusText.Text = "默认分类不能删除";
            return;
        }

        if (!_config.Categories.Contains(category))
        {
            StatusText.Text = "分类不存在";
            return;
        }

        var contained = _config.Symbols.Where(symbol => symbol.Category == category).ToList();
        if (contained.Count > 0)
        {
            var result = MessageBox.Show(
                this,
                $"分类“{category}”里还有 {contained.Count} 个标的，删除后会移入默认分类。继续吗？",
                "删除分类",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (var symbol in contained)
            {
                symbol.Category = DefaultCategory;
            }
        }

        _config.Categories.Remove(category);
        StatusText.Text = "已删除分类";
        SaveConfig();
        Render();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void TopmostButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        StatusText.Text = Topmost ? "已置顶" : "已取消置顶";
        SaveConfig();
        ApplyTopmostButtonStyle();
    }

    private void ResizeHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _resizeStartScreen = PointToScreen(e.GetPosition(this));
        _resizeStartWidth = Width;
        _resizeStartHeight = Height;
        ResizeHandle.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ResizeHandle.IsMouseCaptured)
        {
            ResizeHandle.ReleaseMouseCapture();
        }
    }

    private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!ResizeHandle.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentScreen = PointToScreen(e.GetPosition(this));
        var delta = currentScreen - _resizeStartScreen;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is not null)
        {
            delta = source.CompositionTarget.TransformFromDevice.Transform(delta);
        }

        Width = Math.Max(MinWidth, _resizeStartWidth + delta.X);
        Height = Math.Max(MinHeight, _resizeStartHeight + delta.Y);
    }

    private void ApplyTopmostButtonStyle()
    {
        TopmostButton.ToolTip = Topmost ? "当前置顶，点击取消置顶" : "当前可被遮挡，点击置顶";
        TopmostButton.Background = Topmost ? ColorBrush(120, 108, 180, 255) : Brushes.Transparent;
        TopmostButton.Foreground = Topmost ? ColorBrush(255, 248, 250, 252) : TextBrush;
        TopmostButton.BorderBrush = Topmost ? ColorBrush(110, 150, 205, 255) : Brushes.Transparent;
        TopmostButton.BorderThickness = Topmost ? new Thickness(1) : new Thickness(0);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _isLight = !_isLight;
        SaveConfig();
        ApplyTheme();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing)
        {
            return;
        }
        _config.Appearance.Opacity = Math.Clamp(OpacitySlider.Value / 100, 0.35, 1.0);
        OpacityText.Text = $"{(int)OpacitySlider.Value}%";
        SaveConfig();
        ApplyTheme();
    }

    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 || IsInside<Button>(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse state changes during a rapid click.
        }
    }

    private void Row_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: SymbolItem symbol })
        {
            return;
        }
        _selectedCode = symbol.Code;
        Render();
    }

    private void Row_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: SymbolItem symbol })
        {
            _selectedCode = symbol.Code;
        }
    }

    private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SymbolItem symbol })
        {
            return;
        }

        _selectedCode = symbol.Code;
        _dragStart = e.GetPosition(this);
        if (sender is UIElement element)
        {
            element.CaptureMouse();
        }
        e.Handled = true;
    }

    private void DragHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement { IsMouseCaptured: true } element)
        {
            element.ReleaseMouseCapture();
        }
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { Tag: SymbolItem symbol })
        {
            return;
        }
        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < 4 && Math.Abs(position.Y - _dragStart.Y) < 4)
        {
            return;
        }
        var data = new DataObject();
        data.SetData(SymbolDragFormat, symbol.Code);
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            if (sender is UIElement { IsMouseCaptured: true } element)
            {
                element.ReleaseMouseCapture();
            }
        }
        e.Handled = true;
    }

    private void CategoryHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string category })
        {
            return;
        }

        _dragStart = e.GetPosition(this);
        if (sender is UIElement element)
        {
            element.CaptureMouse();
        }
        e.Handled = true;
    }

    private void CategoryHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement { IsMouseCaptured: true } element)
        {
            element.ReleaseMouseCapture();
        }
    }

    private void CategoryHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { Tag: string category })
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < 4 && Math.Abs(position.Y - _dragStart.Y) < 4)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(CategoryDragFormat, category);
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            if (sender is UIElement { IsMouseCaptured: true } element)
            {
                element.ReleaseMouseCapture();
            }
        }
        e.Handled = true;
    }

    private void Row_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Border { Tag: SymbolItem target } || !TryGetSymbolDragCode(e, out var code))
        {
            return;
        }
        MoveSymbol(code, target.Category, target.Code);
        e.Handled = true;
    }

    private void Category_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Border { Tag: string category })
        {
            return;
        }

        if (TryGetCategoryDragName(e, out var movingCategory))
        {
            MoveCategory(movingCategory, category);
            e.Handled = true;
            return;
        }

        if (TryGetSymbolDragCode(e, out var code))
        {
            MoveSymbol(code, category, null);
            e.Handled = true;
        }
    }

    private static bool TryGetSymbolDragCode(DragEventArgs e, out string code)
    {
        if (e.Data.GetDataPresent(SymbolDragFormat))
        {
            code = (string)e.Data.GetData(SymbolDragFormat)!;
            return true;
        }

        if (e.Data.GetDataPresent(typeof(string)))
        {
            code = (string)e.Data.GetData(typeof(string))!;
            return true;
        }

        code = "";
        return false;
    }

    private static bool TryGetCategoryDragName(DragEventArgs e, out string category)
    {
        if (e.Data.GetDataPresent(CategoryDragFormat))
        {
            category = (string)e.Data.GetData(CategoryDragFormat)!;
            return true;
        }

        category = "";
        return false;
    }

    private void MoveCategory(string movingCategory, string targetCategory)
    {
        if (movingCategory == targetCategory)
        {
            return;
        }

        var originalIndex = _config.Categories.IndexOf(movingCategory);
        var targetIndex = _config.Categories.IndexOf(targetCategory);
        if (originalIndex < 0 || targetIndex < 0)
        {
            return;
        }

        var insertAfterTarget = originalIndex < targetIndex;
        _config.Categories.RemoveAt(originalIndex);

        targetIndex = _config.Categories.IndexOf(targetCategory);
        if (targetIndex < 0)
        {
            _config.Categories.Add(movingCategory);
        }
        else
        {
            var insertIndex = insertAfterTarget ? targetIndex + 1 : targetIndex;
            _config.Categories.Insert(Math.Clamp(insertIndex, 0, _config.Categories.Count), movingCategory);
        }

        StatusText.Text = "已调整分类顺序";
        SaveConfig();
        Render();
    }

    private void MoveSymbol(string code, string category, string? beforeCode)
    {
        var moving = _config.Symbols.FirstOrDefault(symbol => symbol.Code == code);
        if (moving is null)
        {
            return;
        }

        _config.Symbols.Remove(moving);
        moving.Category = category;
        if (!_config.Categories.Contains(category))
        {
            _config.Categories.Add(category);
        }

        var insertIndex = beforeCode is null ? -1 : _config.Symbols.FindIndex(symbol => symbol.Code == beforeCode);
        if (insertIndex >= 0)
        {
            _config.Symbols.Insert(insertIndex, moving);
        }
        else
        {
            var lastInCategory = _config.Symbols.FindLastIndex(symbol => symbol.Category == category);
            _config.Symbols.Insert(lastInCategory >= 0 ? lastInCategory + 1 : _config.Symbols.Count, moving);
        }

        _selectedCode = code;
        SaveConfig();
        Render();
    }

    private byte SurfaceAlpha(byte minimumAlpha)
    {
        var t = (AppearanceOpacity - 0.35) / 0.65;
        return (byte)Math.Round(minimumAlpha + (255 - minimumAlpha) * t);
    }

    private static SolidColorBrush ColorBrush(byte alpha, byte red, byte green, byte blue) => new(Color.FromArgb(alpha, red, green, blue));

    private static bool IsInside<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T)
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                yield return typed;
            }
            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}

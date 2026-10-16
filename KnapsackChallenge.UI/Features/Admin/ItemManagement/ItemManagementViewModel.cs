using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Admin
{
    // ----- v10: Wrapper cho popup chọn icon -----
    // Gom theo Category ở tầng UI: mỗi IconGroup = 1 nhóm, Icons = các icon trong nhóm.
    // Mục đích: XAML dùng ItemsControl lồng nhau, WrapPanel cho nhóm con.
    public sealed class IconGroup
    {
        public string Category { get; }
        public IReadOnlyList<IconChoiceViewModel> Icons { get; }
        public IconGroup(string category, IReadOnlyList<IconChoiceViewModel> icons)
        {
            Category = category;
            Icons = icons;
        }
    }

    // 1 ô icon trong popup. IsSelected do VM đặt lại mỗi khi SelectedIconKey đổi.
    // PickCommand = callback để popup chọn và đóng ngay tại chỗ (không cần ancestor binding
    // — hữu ích vì popup detach khỏi visual tree, RelativeSource FindAncestor không đáng tin).
    public sealed class IconChoiceViewModel : ViewModelBase
    {
        public ItemIconInfo Info { get; }
        public string Key => Info.Key;
        public string DisplayName => Info.DisplayName;
        public string Category => Info.Category;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        public ICommand PickCommand { get; }

        public IconChoiceViewModel(ItemIconInfo info, Action<string> onPick)
        {
            Info = info;
            PickCommand = new RelayCommand<object>(_ => onPick(Info.Key));
        }
    }

    public class ItemManagementViewModel : ViewModelBase
    {
        private readonly IItemService _itemService;
        private readonly ICollectionView _itemsView;

        private ItemEntity? _selectedItem;
        private string _itemName = "";
        private string _weightText = "";
        private string _valueText = "";
        private string _searchText = "";
        private string _errorMessage = "";
        private string _infoMessage = "";

        // ----- v10: trạng thái icon -----
        private string? _selectedIconKey;   // null = dùng icon "default"
        private string _iconSearchText = "";
        private bool _isIconPickerOpen;

        public ObservableCollection<ItemEntity> Items { get; } = new();

        // Nhóm icon đã lọc. Mỗi lần search hoặc đổi SelectedIconKey -> dựng lại.
        // (Số icon chỉ ~60 nên rebuild rẻ, đổi lại code đơn giản + không lo cache selection.)
        public ObservableCollection<IconGroup> FilteredIcons { get; } = new();

        public ItemEntity? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormTitle));

                // Chọn dòng nào thì đổ dữ liệu dòng đó lên form để sửa
                if (value != null)
                {
                    ItemName = value.Name;
                    WeightText = value.Weight.ToString();
                    ValueText = value.Value.ToString();
                    SelectedIconKey = value.IconKey; // v10
                    ErrorMessage = "";
                }
            }
        }

        public string FormTitle => SelectedItem == null ? "Thêm vật phẩm mới" : $"Sửa vật phẩm #{SelectedItem.Id}";

        public string ItemName
        {
            get => _itemName;
            set { _itemName = value; OnPropertyChanged(); }
        }

        public string WeightText
        {
            get => _weightText;
            set { _weightText = value; OnPropertyChanged(); }
        }

        public string ValueText
        {
            get => _valueText;
            set { _valueText = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); _itemsView.Refresh(); }
        }

        // ----- v10: icon -----

        // null/rỗng = dùng icon "default" (khớp semantic của ItemEntity.IconKey).
        public string? SelectedIconKey
        {
            get => _selectedIconKey;
            set
            {
                if (string.Equals(_selectedIconKey, value, StringComparison.Ordinal)) return;
                _selectedIconKey = value;
                OnPropertyChanged();
                RebuildIconGroups(); // cập nhật viền "đang chọn" trong popup
            }
        }

        public string IconSearchText
        {
            get => _iconSearchText;
            set
            {
                if (string.Equals(_iconSearchText, value, StringComparison.Ordinal)) return;
                _iconSearchText = value;
                OnPropertyChanged();
                RebuildIconGroups();
            }
        }

        // TwoWay với Popup.IsOpen (StaysOpen=False). Popup tự set false khi click ra ngoài.
        public bool IsIconPickerOpen
        {
            get => _isIconPickerOpen;
            set { _isIconPickerOpen = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        // v10
        public ICommand OpenIconPickerCommand { get; }
        public ICommand ClearIconCommand { get; }

        private readonly IDialogService _dialog;
        public ItemManagementViewModel(IDialogService? dialog = null)
        {
            _itemService = ServiceFactory.GetItemService();
            _dialog = dialog ?? new WpfDialogService();

            // Bộ lọc tìm kiếm theo tên
            _itemsView = CollectionViewSource.GetDefaultView(Items);
            _itemsView.Filter = o =>
                string.IsNullOrWhiteSpace(SearchText) ||
                (o is ItemEntity item && item.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

            AddCommand = new RelayCommand<object>(_ => ExecuteAdd());
            UpdateCommand = new RelayCommand<object>(_ => ExecuteUpdate(), _ => SelectedItem != null);
            DeleteCommand = new RelayCommand<object>(_ => ExecuteDelete(), _ => SelectedItem != null);
            ClearCommand = new RelayCommand<object>(_ => ClearForm());
            // v10
            OpenIconPickerCommand = new RelayCommand<object>(_ => IsIconPickerOpen = true);
            ClearIconCommand = new RelayCommand<object>(_ => SelectedIconKey = null);

            RebuildIconGroups(); // nạp danh sách icon cho popup ngay từ đầu
            LoadItems();
        }

        // ----- v10: dựng lại FilteredIcons theo IconSearchText + trạng thái chọn -----

        private void RebuildIconGroups()
        {
            var search = (_iconSearchText ?? "").Trim();

            // Chuẩn hoá selection để so khớp với key trong catalog.
            // null/rỗng -> "default" (để viền highlight rơi đúng vào ô default khi chưa chọn gì).
            var selectedNormalized = string.IsNullOrWhiteSpace(_selectedIconKey)
                ? ItemIconCatalog.DefaultKey
                : ItemIconCatalog.Normalize(_selectedIconKey);

            IEnumerable<ItemIconInfo> source = ItemIconCatalog.All;
            if (search.Length > 0)
            {
                source = source.Where(i =>
                    i.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    i.Key.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            // Tạo IconChoiceViewModel với callback Pick: set key + đóng popup.
            var groups = source
                .GroupBy(i => i.Category)
                .Select(g => new IconGroup(
                    g.Key,
                    g.Select(i => new IconChoiceViewModel(i, OnIconPicked)
                    {
                        IsSelected = string.Equals(i.Key, selectedNormalized, StringComparison.OrdinalIgnoreCase),
                    }).ToList()))
                .ToList();

            FilteredIcons.Clear();
            foreach (var g in groups) FilteredIcons.Add(g);
        }

        private void OnIconPicked(string key)
        {
            SelectedIconKey = key;
            IsIconPickerOpen = false; // đóng popup ngay khi chọn
        }

        // ----- Load / CRUD -----

        private void LoadItems()
        {
            try
            {
                Items.Clear();
                foreach (var item in _itemService.GetAll())
                    Items.Add(item);
            }
            catch (Exception)
            {
                ErrorMessage = "Không kết nối được cơ sở dữ liệu! Kiểm tra lại connection string.";
            }
        }

        // Đọc 2 ô số từ TextBox; trả về false và báo lỗi nếu nhập sai
        private bool TryReadNumbers(out int weight, out int value)
        {
            value = 0;

            if (!int.TryParse(WeightText, out weight) || !int.TryParse(ValueText, out value))
            {
                ErrorMessage = "Khối lượng và giá trị phải là số nguyên!";
                return false;
            }

            return true;
        }

        private void ExecuteAdd()
        {
            ClearMessages();

            if (!TryReadNumbers(out int weight, out int value))
            {
                return;
            }

            try
            {
                // v10: truyền SelectedIconKey (null = icon "default").
                var (success, message) = _itemService.Add(ItemName, weight, value, SelectedIconKey);

                if (!success)
                {
                    ErrorMessage = message;
                    return;
                }

                LoadItems();
                ClearForm();

                InfoMessage = message;
            }
            catch (Exception)
            {
                ErrorMessage = "Lỗi cơ sở dữ liệu khi thêm vật phẩm!";
            }
        }

        private void ExecuteUpdate()
        {
            ClearMessages();

            if (SelectedItem == null || !TryReadNumbers(out int weight, out int value))
            {
                return;
            }

            try
            {
                // v10: truyền SelectedIconKey.
                var (success, message) = _itemService.Update(
                    SelectedItem.Id, ItemName, weight, value, SelectedIconKey);

                if (!success)
                {
                    ErrorMessage = message; return;
                }

                LoadItems();
                ClearForm();
                InfoMessage = message;
            }
            catch (Exception)
            {
                ErrorMessage = "Lỗi cơ sở dữ liệu khi cập nhật vật phẩm!";
            }
        }

        private void ExecuteDelete()
        {
            ClearMessages();
            if (SelectedItem == null) return;

            if (!_dialog.Confirm($"Xóa vật phẩm \"{SelectedItem.Name}\"?", "Xác nhận xóa")) return;

            try
            {
                var (success, message) = _itemService.Delete(SelectedItem.Id);
                if (!success) { ErrorMessage = message; return; }

                LoadItems();
                ClearForm();
                InfoMessage = message;
            }
            catch (Exception)
            {
                ErrorMessage = "Lỗi cơ sở dữ liệu khi xóa vật phẩm!";
            }
        }

        private void ClearForm()
        {
            SelectedItem = null;
            ItemName = "";
            WeightText = "";
            ValueText = "";
            SelectedIconKey = null;   // v10: reset icon về "default"
            IsIconPickerOpen = false; // v10: đóng popup nếu đang mở
            IconSearchText = "";      // v10: reset từ khoá tìm icon
            ClearMessages();
        }

        private void ClearMessages()
        {
            ErrorMessage = "";
            InfoMessage = "";
        }
    }
}
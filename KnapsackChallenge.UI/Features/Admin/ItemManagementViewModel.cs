
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Admin
{
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

        public ObservableCollection<ItemEntity> Items { get; } = new();

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

        public ItemManagementViewModel()
        {
            _itemService = ServiceFactory.GetItemService();

            // Bộ lọc tìm kiếm theo tên
            _itemsView = CollectionViewSource.GetDefaultView(Items);
            _itemsView.Filter = o =>
                string.IsNullOrWhiteSpace(SearchText) ||
                (o is ItemEntity item && item.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

            AddCommand = new RelayCommand<object>(_ => ExecuteAdd());
            UpdateCommand = new RelayCommand<object>(_ => ExecuteUpdate(), _ => SelectedItem != null);
            DeleteCommand = new RelayCommand<object>(_ => ExecuteDelete(), _ => SelectedItem != null);
            ClearCommand = new RelayCommand<object>(_ => ClearForm());

            LoadItems();
        }

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
                var (success, message) = _itemService.Add(ItemName, weight, value);

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
                var (success, message) = _itemService.Update(SelectedItem.Id, ItemName, weight, value);
               
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

            var confirm = MessageBox.Show(
                $"Xóa vật phẩm \"{SelectedItem.Name}\"?", "Xác nhận xóa",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

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
        }

        private void ClearMessages()
        {
            ErrorMessage = "";
            InfoMessage = "";
        }
    }
}
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Admin
{
    public class SetManagementViewModel : ViewModelBase
    {
        private readonly ISetService _setService;
        private readonly IItemService _itemService;
        private readonly IDialogService _dialog;

        // ====== Danh sách bộ đề ======
        public ObservableCollection<KnapsackSetEntity> Sets { get; } = new();

        // ====== Kho vật phẩm (bên trái picker) và Vật phẩm trong bộ đề (bên phải) ======
        public ObservableCollection<ItemEntity> AvailableItems { get; } = new();
        public ObservableCollection<ItemEntity> SelectedItems { get; } = new();

        private readonly ICollectionView _availableView;
        private readonly ICollectionView _selectedView;

        private KnapsackSetEntity? _selectedSet;
        private ItemEntity? _availableSelected;
        private ItemEntity? _selectedSelected;

        private string _setName = "";
        private string _maxWeightText = "";
        private string _difficulty = "Easy";
        private string _availableSearch = "";
        private string _selectedSearch = "";
        private string _errorMessage = "";
        private string _infoMessage = "";
        private bool _isLoading;

        public SetManagementViewModel()
        {
            _setService = ServiceFactory.GetSetService();
            _itemService = ServiceFactory.GetItemService();
            _dialog = new WpfDialogService();

            _availableView = CollectionViewSource.GetDefaultView(AvailableItems);
            _availableView.Filter = o =>
                string.IsNullOrWhiteSpace(AvailableSearch) ||
                (o is ItemEntity it && it.Name.Contains(AvailableSearch.Trim(), StringComparison.OrdinalIgnoreCase));

            _selectedView = CollectionViewSource.GetDefaultView(SelectedItems);
            _selectedView.Filter = o =>
                string.IsNullOrWhiteSpace(SelectedSearch) ||
                (o is ItemEntity it && it.Name.Contains(SelectedSearch.Trim(), StringComparison.OrdinalIgnoreCase));

            AddSetCommand = new RelayCommand<object>(_ => ExecuteAddSet());
            UpdateSetCommand = new RelayCommand<object>(_ => ExecuteUpdateSet(), _ => SelectedSet != null);
            DeleteSetCommand = new RelayCommand<object>(_ => ExecuteDeleteSet(), _ => SelectedSet != null);
            ClearSetFormCommand = new RelayCommand<object>(_ => ClearSetForm());

            AddItemCommand = new RelayCommand<object>(_ => MoveAvailableToSelected(), _ => AvailableSelected != null);
            RemoveItemCommand = new RelayCommand<object>(_ => MoveSelectedToAvailable(), _ => SelectedSelected != null);
            AddAllCommand = new RelayCommand<object>(_ => MoveAllAvailableToSelected(), _ => AvailableItems.Count > 0);
            RemoveAllCommand = new RelayCommand<object>(_ => MoveAllSelectedToAvailable(), _ => SelectedItems.Count > 0);

            SaveItemsCommand = new RelayCommand<object>(_ => ExecuteSaveItems(), _ => SelectedSet != null);

            LoadAll();
        }

        // ================== Properties ==================

        public KnapsackSetEntity? SelectedSet
        {
            get => _selectedSet;
            set
            {
                if (ReferenceEquals(_selectedSet, value)) return;
                _selectedSet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormTitle));
                OnPropertyChanged(nameof(HasSelectedSet));

                if (value != null)
                {
                    SetName = value.SetName;
                    MaxWeightText = value.MaxWeight.ToString();
                    Difficulty = string.IsNullOrEmpty(value.Difficulty) ? "Easy" : value.Difficulty;
                    ErrorMessage = "";
                    InfoMessage = "";
                }

                LoadItemsForCurrentSet();
                UpdateTotals();
            }
        }

        public bool HasSelectedSet => SelectedSet != null;

        public string FormTitle => SelectedSet == null ? "Thêm bộ đề mới" : $"Sửa bộ đề #{SelectedSet.Id}";

        public ItemEntity? AvailableSelected
        {
            get => _availableSelected;
            set { _availableSelected = value; OnPropertyChanged(); }
        }

        public ItemEntity? SelectedSelected
        {
            get => _selectedSelected;
            set { _selectedSelected = value; OnPropertyChanged(); }
        }

        public string SetName
        {
            get => _setName;
            set { _setName = value; OnPropertyChanged(); }
        }

        public string MaxWeightText
        {
            get => _maxWeightText;
            set { _maxWeightText = value; OnPropertyChanged(); }
        }

        public string Difficulty
        {
            get => _difficulty;
            set { _difficulty = value; OnPropertyChanged(); }
        }

        // 3 lựa chọn cho ComboBox
        public string[] DifficultyOptions { get; } = { "Easy", "Medium", "Hard" };

        public string AvailableSearch
        {
            get => _availableSearch;
            set { _availableSearch = value; OnPropertyChanged(); _availableView.Refresh(); }
        }

        public string SelectedSearch
        {
            get => _selectedSearch;
            set { _selectedSearch = value; OnPropertyChanged(); _selectedView.Refresh(); }
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

        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        // ===== Thống kê bộ đề đang chọn =====
        public int TotalItemCount => SelectedItems.Count;
        public int TotalWeight => SelectedItems.Sum(i => i.Weight);
        public int TotalValue => SelectedItems.Sum(i => i.Value);
        public int MaxWeight => SelectedSet?.MaxWeight ?? 0;

        public bool IsOverWeight => SelectedSet != null && TotalWeight > SelectedSet.MaxWeight;

        public string TotalSummaryText =>
            SelectedSet == null
                ? "Chưa chọn bộ đề"
                : $"{TotalItemCount} vật phẩm  •  {TotalWeight}/{MaxWeight} khối lượng  •  {TotalValue} giá trị";

        // ================== Commands ==================

        public ICommand AddSetCommand { get; }
        public ICommand UpdateSetCommand { get; }
        public ICommand DeleteSetCommand { get; }
        public ICommand ClearSetFormCommand { get; }

        public ICommand AddItemCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand AddAllCommand { get; }
        public ICommand RemoveAllCommand { get; }

        public ICommand SaveItemsCommand { get; }

        // ================== Load / thao tác ==================

        private void LoadAll()
        {
            try
            {
                IsLoading = true;

                // Kho vật phẩm (dùng chung cho picker)
                var allItems = _itemService.GetAll();

                // Danh sách bộ đề
                Sets.Clear();
                foreach (var s in _setService.GetAll()) Sets.Add(s);

                // Nếu đang chọn set nào thì giữ nguyên lựa chọn, ngược lại bỏ chọn
                SelectedSet = null;

                // Ghi nhớ toàn bộ kho để rebuild AvailableItems khi cần
                _allItemsCache = allItems;

                // Chưa chọn set → cột phải để trống
                AvailableItems.Clear();
                SelectedItems.Clear();
                UpdateTotals();
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private System.Collections.Generic.List<ItemEntity> _allItemsCache = new();

        private void LoadItemsForCurrentSet()
        {
            AvailableItems.Clear();
            SelectedItems.Clear();

            if (SelectedSet == null)
            {
                _availableView.Refresh();
                _selectedView.Refresh();
                return;
            }

            // Id các vật phẩm đang nằm trong bộ đề
            var selectedIds = _setService.GetItemIdsInSet(SelectedSet.Id).ToHashSet();

            foreach (var item in _allItemsCache)
            {
                if (selectedIds.Contains(item.Id))
                    SelectedItems.Add(item);
                else
                    AvailableItems.Add(item);
            }

            _availableView.Refresh();
            _selectedView.Refresh();
        }

        private void MoveAvailableToSelected()
        {
            if (AvailableSelected == null) return;
            var item = AvailableSelected;
            AvailableItems.Remove(item);
            if (!SelectedItems.Any(i => i.Id == item.Id)) SelectedItems.Add(item);
            AvailableSelected = null;
            UpdateTotals();
        }

        private void MoveSelectedToAvailable()
        {
            if (SelectedSelected == null) return;
            var item = SelectedSelected;
            SelectedItems.Remove(item);
            if (!AvailableItems.Any(i => i.Id == item.Id)) AvailableItems.Add(item);
            SelectedSelected = null;
            UpdateTotals();
        }

        private void MoveAllAvailableToSelected()
        {
            foreach (var item in AvailableItems.ToList())
            {
                AvailableItems.Remove(item);
                if (!SelectedItems.Any(i => i.Id == item.Id)) SelectedItems.Add(item);
            }
            UpdateTotals();
        }

        private void MoveAllSelectedToAvailable()
        {
            foreach (var item in SelectedItems.ToList())
            {
                SelectedItems.Remove(item);
                if (!AvailableItems.Any(i => i.Id == item.Id)) AvailableItems.Add(item);
            }
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            OnPropertyChanged(nameof(TotalItemCount));
            OnPropertyChanged(nameof(TotalWeight));
            OnPropertyChanged(nameof(TotalValue));
            OnPropertyChanged(nameof(MaxWeight));
            OnPropertyChanged(nameof(IsOverWeight));
            OnPropertyChanged(nameof(TotalSummaryText));
        }

        // ---------- CRUD bộ đề ----------

        private bool TryReadMaxWeight(out int maxWeight)
        {
            if (!int.TryParse(MaxWeightText, out maxWeight))
            {
                ErrorMessage = "Khối lượng tối đa phải là số nguyên!";
                return false;
            }
            return true;
        }

        private void ExecuteAddSet()
        {
            ClearMessages();
            if (!TryReadMaxWeight(out int mw)) return;

            try
            {
                var (ok, msg) = _setService.Add(SetName, mw, Difficulty);
                if (!ok) { ErrorMessage = msg; return; }

                LoadAll();
                InfoMessage = msg;
            }
            catch (SqlException) { ErrorMessage = "Lỗi cơ sở dữ liệu khi thêm bộ đề!"; }
        }

        private void ExecuteUpdateSet()
        {
            ClearMessages();
            if (SelectedSet == null || !TryReadMaxWeight(out int mw)) return;

            try
            {
                var (ok, msg) = _setService.Update(SelectedSet.Id, SetName, mw, Difficulty);
                if (!ok) { ErrorMessage = msg; return; }

                var keepId = SelectedSet.Id;
                LoadAll();
                SelectedSet = Sets.FirstOrDefault(s => s.Id == keepId);
                InfoMessage = msg;
            }
            catch (SqlException) { ErrorMessage = "Lỗi cơ sở dữ liệu khi cập nhật bộ đề!"; }
        }

        private void ExecuteDeleteSet()
        {
            ClearMessages();
            if (SelectedSet == null) return;

            if (!_dialog.Confirm($"Xóa bộ đề \"{SelectedSet.SetName}\"?", "Xác nhận xóa")) return;

            try
            {
                var (ok, msg) = _setService.Delete(SelectedSet.Id);
                if (!ok) { ErrorMessage = msg; return; }

                LoadAll();
                InfoMessage = msg;
            }
            catch (SqlException) { ErrorMessage = "Lỗi cơ sở dữ liệu khi xóa bộ đề!"; }
        }

        private void ClearSetForm()
        {
            SelectedSet = null;
            SetName = "";
            MaxWeightText = "";
            Difficulty = "Easy";
            ClearMessages();
        }

        // ---------- Lưu SetItems ----------

        private void ExecuteSaveItems()
        {
            ClearMessages();
            if (SelectedSet == null) return;

            try
            {
                var ids = SelectedItems.Select(i => i.Id).ToList();
                var (ok, msg) = _setService.ReplaceSetItems(SelectedSet.Id, ids);
                if (!ok) { ErrorMessage = msg; return; }

                InfoMessage = msg;
            }
            catch (SqlException) { ErrorMessage = "Lỗi cơ sở dữ liệu khi lưu danh sách vật phẩm!"; }
        }

        private void ClearMessages()
        {
            ErrorMessage = "";
            InfoMessage = "";
        }
    }
}
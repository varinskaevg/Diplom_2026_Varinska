using System.Windows;
using System.Windows.Controls;
using FitnessClub.Desktop.Services;
using FitnessClub.Desktop.Models;

namespace FitnessClub.Desktop.Views.Pages;

public partial class PaymentsPage : Page
{
    private List<PaymentItem> _all = [];

    public PaymentsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadPayments();
    }

    private async Task LoadPayments()
    {
        try
        {
            var stats = await ApiClient.GetAsync<DashboardStats>("api/dashboard");
            if (stats != null)
                MonthRevenueText.Text = $"{stats.MonthRevenue:N0} ₴";

            var payments = await ApiClient.GetAsync<List<PaymentItem>>("api/payments");
            if (payments == null) return;
            _all = payments;
            PaymentsList.ItemsSource = _all;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка: {ex.Message}");
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_all == null || PaymentsList == null) return;

        PaymentsList.ItemsSource =
            FilterAll.IsChecked == true ? _all :
            FilterCash.IsChecked == true ? _all.Where(p => p.PaymentMethod == "Cash" && !p.IsRefund).ToList() :
            FilterCard.IsChecked == true ? _all.Where(p => p.PaymentMethod == "Card" && !p.IsRefund).ToList() :
            FilterOnline.IsChecked == true ? _all.Where(p => p.PaymentMethod == "Online" && !p.IsRefund).ToList() :
            _all.Where(p => p.IsRefund).ToList();
    }
}

public class PaymentItem
{
    public int Id { get; set; }
    public string ClientName { get; set; } = "";
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentDateStr => PaymentDate.ToString("dd.MM.yyyy HH:mm");
    public string PaymentMethodUa => PaymentMethod switch
    {
        "Cash" => "Готівка",
        "Card" => "Картка",
        "Online" => "Онлайн",
        _ => PaymentMethod ?? ""
    };
    public bool IsRefund => Amount < 0;
    public string RefundBadgeVisible => IsRefund ? "Visible" : "Collapsed";
}
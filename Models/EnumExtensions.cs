using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Models;

public static class EnumExtensions
{
    /// <summary>Libellé français défini par [Display(Name = …)].</summary>
    public static string Label(this Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();

    /// <summary>Couleur de badge (classe badge-soft) pour un statut de commande.</summary>
    public static string BadgeColor(this OrderStatus s) => s switch
    {
        OrderStatus.Pending => "amber",
        OrderStatus.Confirmed => "blue",
        OrderStatus.Preparing => "violet",
        OrderStatus.Shipping => "blue",
        OrderStatus.Delivered => "green",
        _ => "red",
    };

    public static string Icon(this OrderStatus s) => s switch
    {
        OrderStatus.Pending => "bi-hourglass-split",
        OrderStatus.Confirmed => "bi-check2-circle",
        OrderStatus.Preparing => "bi-box-seam",
        OrderStatus.Shipping => "bi-truck",
        OrderStatus.Delivered => "bi-house-check",
        _ => "bi-x-circle",
    };
}

using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rap.Studio
{
    /// <summary>统一的表单控件工厂（保持简洁浅色风格）。</summary>
    public static class W
    {
        public static ComboBox Combo(IList items, int index)
        {
            ComboBox cb = new ComboBox();
            cb.ItemsSource = items;
            cb.SelectedIndex = index;
            cb.Height = 30;
            cb.MinWidth = 190;
            cb.FontSize = 12;
            cb.FontFamily = Theme.UiFont;
            cb.VerticalContentAlignment = VerticalAlignment.Center;
            cb.Background = new SolidColorBrush(Colors.White);
            cb.BorderBrush = Theme.Border;
            cb.Padding = new Thickness(6, 0, 6, 0);
            StyleCombo(cb);
            return cb;
        }

        private static bool _comboStyleReady;
        private static Style _comboBoxStyle;
        private static Style _comboItemStyle;

        private static void StyleCombo(ComboBox cb)
        {
            if (!_comboStyleReady)
            {
                _comboStyleReady = true;
                try
                {
                    string itemXaml =
                        "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBoxItem'>" +
                        "<Setter Property='Template'><Setter.Value>" +
                        "<ControlTemplate TargetType='ComboBoxItem'>" +
                        "<Border x:Name='ib' Background='Transparent' Padding='9,5,9,5' CornerRadius='4'>" +
                        "<ContentPresenter/></Border>" +
                        "<ControlTemplate.Triggers>" +
                        "<Trigger Property='IsHighlighted' Value='True'><Setter TargetName='ib' Property='Background' Value='#F2F3F5'/></Trigger>" +
                        "<Trigger Property='IsSelected' Value='True'><Setter TargetName='ib' Property='Background' Value='#FFEDE7'/></Trigger>" +
                        "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>";
                    _comboItemStyle = (Style)System.Windows.Markup.XamlReader.Parse(itemXaml);

                    string tpl =
                        "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'>" +
                        "<Setter Property='Template'><Setter.Value>" +
                        "<ControlTemplate TargetType='ComboBox'>" +
                        "<Grid>" +
                        "<Border x:Name='bd' Background='White' BorderBrush='#E5E7EB' BorderThickness='1' CornerRadius='6'/>" +
                        "<ContentPresenter Margin='10,0,24,0' VerticalAlignment='Center' HorizontalAlignment='Left'" +
                        " Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'/>" +
                        "<Path Data='M0,0 L4.5,4.5 L9,0' Stroke='#6E7681' StrokeThickness='1.3' HorizontalAlignment='Right'" +
                        " VerticalAlignment='Center' Margin='0,0,10,0'/>" +
                        "<ToggleButton IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'" +
                        " ClickMode='Press' Background='Transparent' BorderThickness='0' Focusable='False'/>" +
                        "<Popup IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False'>" +
                        "<Border Background='White' BorderBrush='#E5E7EB' BorderThickness='1' CornerRadius='6' Margin='0,4,0,0'" +
                        " MinWidth='{TemplateBinding ActualWidth}' MaxHeight='240'>" +
                        "<ScrollViewer><ItemsPresenter Margin='4'/></ScrollViewer></Border></Popup>" +
                        "</Grid>" +
                        "<ControlTemplate.Triggers>" +
                        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#D3D7DE'/></Trigger>" +
                        "<Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.55'/></Trigger>" +
                        "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>" +
                        "</Style>";
                    _comboBoxStyle = (Style)System.Windows.Markup.XamlReader.Parse(tpl);
                }
                catch { _comboBoxStyle = null; _comboItemStyle = null; }
            }
            if (_comboBoxStyle != null) cb.Style = _comboBoxStyle;
            if (_comboItemStyle != null && cb.ItemContainerStyle == null) cb.ItemContainerStyle = _comboItemStyle;
        }

        public static CheckBox Check(string text, bool value)
        {
            CheckBox cb = new CheckBox();
            cb.Content = text;
            cb.IsChecked = value;
            cb.FontSize = 12.5;
            cb.FontFamily = Theme.UiFont;
            cb.Foreground = Theme.Text;
            cb.VerticalContentAlignment = VerticalAlignment.Center;
            cb.Margin = new Thickness(0, 4, 0, 4);
            return cb;
        }

        public static TextBox Text(string value, double width)
        {
            TextBox tb = new TextBox();
            tb.Text = value;
            tb.Width = width;
            tb.Height = 30;
            tb.FontSize = 12;
            tb.FontFamily = Theme.UiFont;
            tb.VerticalContentAlignment = VerticalAlignment.Center;
            tb.Padding = new Thickness(8, 0, 8, 0);
            tb.BorderBrush = Theme.Border;
            tb.Background = new SolidColorBrush(Colors.White);
            return tb;
        }

        public static Slider Slide(double min, double max, double value, double width)
        {
            Slider s = new Slider();
            s.Minimum = min;
            s.Maximum = max;
            s.Value = value;
            s.Width = width;
            s.IsSnapToTickEnabled = true;
            s.TickFrequency = 1;
            s.VerticalAlignment = VerticalAlignment.Center;
            return s;
        }

        /// <summary>标签 + 控件 的横向行。</summary>
        public static Grid Inline(UIElement left, UIElement right, double leftWidth)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 5, 0, 5);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(leftWidth);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);
            if (left is FrameworkElement)
            {
                FrameworkElement lf = (FrameworkElement)left;
                lf.VerticalAlignment = VerticalAlignment.Center;
                lf.HorizontalAlignment = HorizontalAlignment.Left;
                g.Children.Add(lf);
            }
            else if (left != null)
            {
                g.Children.Add(left);
            }
            if (right is FrameworkElement)
            {
                FrameworkElement rf = (FrameworkElement)right;
                rf.VerticalAlignment = VerticalAlignment.Center;
                rf.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetColumn(rf, 1);
                g.Children.Add(rf);
            }
            else if (right != null)
            {
                Grid.SetColumn(right, 1);
                g.Children.Add(right);
            }
            return g;
        }
    }
}

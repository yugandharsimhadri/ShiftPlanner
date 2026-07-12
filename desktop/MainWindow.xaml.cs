using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ShiftPlanner.Desktop.ViewModels;

namespace ShiftPlanner.Desktop;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private const string CellTemplateXaml = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Grid DataContext="{Binding Days[__INDEX__]}" Margin="4,3">
            <ToggleButton x:Name="Toggle" IsChecked="{Binding IsPopupOpen, Mode=TwoWay}"
                          Content="{Binding Label}" Background="{Binding Background}" Foreground="{Binding Foreground}"
                          Padding="0" Height="26" Cursor="Hand" FontFamily="Consolas" FontSize="11" FontWeight="SemiBold">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Border x:Name="Bd" Background="{TemplateBinding Background}" CornerRadius="4"
                          BorderThickness="0" BorderBrush="#A5392B">
                    <Border.Style>
                      <Style TargetType="Border">
                        <Style.Triggers>
                          <DataTrigger Binding="{Binding IsFlagged}" Value="True">
                            <Setter Property="BorderThickness" Value="1.5"/>
                          </DataTrigger>
                        </Style.Triggers>
                      </Style>
                    </Border.Style>
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                  </Border>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <Popup IsOpen="{Binding IsChecked, ElementName=Toggle}" StaysOpen="False" Placement="Bottom"
                   PlacementTarget="{Binding ElementName=Toggle}"
                   DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}">
              <Border Background="White" BorderBrush="#BCC9C5" BorderThickness="1" CornerRadius="4" Padding="4">
                <StackPanel>
                  <ItemsControl ItemsSource="{Binding AvailableShiftTypes}">
                    <ItemsControl.ItemTemplate>
                      <DataTemplate>
                        <Button Content="{Binding Name}" Padding="10,6" HorizontalContentAlignment="Left"
                                Background="Transparent" BorderThickness="0" Cursor="Hand"
                                Command="{Binding DataContext.AssignCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                CommandParameter="{Binding Code}"/>
                      </DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                  <Separator/>
                  <Button Content="Clear" Padding="10,6" HorizontalContentAlignment="Left"
                          Background="Transparent" BorderThickness="0" Cursor="Hand"
                          Command="{Binding AssignCommand}"/>
                </StackPanel>
              </Border>
            </Popup>
          </Grid>
        </DataTemplate>
        """;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RebuildDayColumns();
            ViewModel.MonthColumnsChanged += (_, _) => Dispatcher.InvokeAsync(RebuildDayColumns);
        };
    }

    private void RebuildDayColumns()
    {
        while (RosterGrid.Columns.Count > 1)
            RosterGrid.Columns.RemoveAt(RosterGrid.Columns.Count - 1);

        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var i = 0; i < ViewModel.Days.Count; i++)
        {
            var date = ViewModel.Days[i];
            var isToday = date == today;

            var header = new TextBlock
            {
                Text = $"{date:ddd}\n{date.Day}",
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = isToday ? (Brush)Application.Current.Resources["AccentBrush"] : (Brush)Application.Current.Resources["InkFaintBrush"]
            };

            var xaml = CellTemplateXaml.Replace("__INDEX__", i.ToString());
            var context = new ParserContext();
            context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            var template = (DataTemplate)XamlReader.Parse(xaml, context);

            var column = new DataGridTemplateColumn
            {
                Header = header,
                Width = 46,
                CanUserResize = false,
                CellTemplate = template
            };
            if (isToday)
            {
                var cellStyle = new Style(typeof(DataGridCell));
                cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Application.Current.Resources["AccentSoftBrush"]));
                column.CellStyle = cellStyle;
            }

            RosterGrid.Columns.Add(column);
        }
    }
}

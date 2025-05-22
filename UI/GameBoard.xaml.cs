using Natak_Front_end.Controllers;
using Natak_Front_end.Core;
using Natak_Front_end.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; 
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Natak_Front_end
{
    /// <summary>
    /// Interaction logic for GameBoard.xaml
    /// </summary>

    public partial class GameBoard : Window
    {
        private readonly ApiService _apiService;
        private readonly GameController _gameController;
        public readonly TaskCompletionSource<bool> gameCompletedSource = new TaskCompletionSource<bool>();

        private bool isDrawingEnabled = true;

        private Dictionary<(int x, int y), (Polygon hex, TextBlock numberText, Brush color, int number)> boardTiles = new Dictionary<(int x, int y), (Polygon, TextBlock, Brush, int)>();
        private Ellipse thiefIndicator;

        private double HexSize = 70;
        private double HorizontalSpacing;
        private double VerticalSpacing;

        private double VillageSize = 25;
        private double RoadSize = 30;

        private Dictionary<PlayerColour, Brush> GamePieceColours = new Dictionary<PlayerColour, Brush>
        {
            { PlayerColour.Red, Brushes.Red },
            { PlayerColour.Blue, Brushes.Blue },
            { PlayerColour.Orange, Brushes.Orange },
            { PlayerColour.White, Brushes.White }
        };

        public GameBoard(int playerCount)
        {
            InitializeComponent();
            _apiService = new ApiService();
            var gameId = GameManager.Instance.GameId;
            _gameController = new GameController(_apiService, gameId, playerCount);

            GameIdText.Text = $"Game ID: {gameId}";

            VerticalSpacing = HexSize * 2;
            HorizontalSpacing = HexSize * Math.Sqrt(3);

            _gameController.VillageBuilt += OnVillageBuilt;
            _gameController.TownBuilt += OnTownBuilt;
            _gameController.RoadBuilt += OnRoadBuilt;
            _gameController.ThiefMoved += OnThiefMoved;
            _gameController.GameEnded += OnGameEnded;
            _gameController.GameStateUpdated += OnGameStateUpdated;

            DrawBoard();
            InitializeGameAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }


        private async void InitializeGameAsync()
        {
            try
            {
                await _gameController.InitializeBoard();
                await _gameController.StartGameAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //gameCompletedSource.SetResult(true);
                await Dispatcher.InvokeAsync(() => Close());
            }
        }

        private void OnGameEnded()
        {
            gameCompletedSource.SetResult(true);
            Dispatcher.Invoke(() => Close());
        }

        private void OnVillageBuilt(Models.Point point, PlayerColour playerColour)
        {
            double vOffset = 0;
            if (point.y % 2 == 0)
            {
                if (point.x % 2 == 0)
                {
                    vOffset = 0.25;
                }
                else
                {
                    vOffset = 0;
                }
                DrawVillage(HorizontalSpacing * (0.5 + point.x * 0.5), VerticalSpacing * (0.75 + point.y / 2 * 1.5 + vOffset) - VillageSize, VillageSize, GamePieceColours[playerColour]);
            }
            else
            {
                if (point.x % 2 == 0)
                {
                    vOffset = -0.25;
                }
                else
                {
                    vOffset = 0;
                }
                DrawVillage(HorizontalSpacing * (0.5 + point.x * 0.5), VerticalSpacing * (1.75 + (point.y - 1) / 2 * 1.5 + vOffset) - VillageSize, VillageSize, GamePieceColours[playerColour]);
            }
        }

        private void OnTownBuilt(Models.Point point, PlayerColour playerColour)
        {
            double vOffset = 0;
            if (point.y % 2 == 0)
            {
                if (point.x % 2 == 0)
                {
                    vOffset = 0.25;
                }
                else
                {
                    vOffset = 0;
                }
                DrawTown(HorizontalSpacing * (0.5 + point.x * 0.5), VerticalSpacing * (0.75 + point.y / 2 * 1.5 + vOffset) - VillageSize * 2, VillageSize, GamePieceColours[playerColour]);
            }
            else
            {
                if (point.x % 2 == 0)
                {
                    vOffset = -0.25;
                }
                else
                {
                    vOffset = 0;
                }
                DrawTown(HorizontalSpacing * (0.5 + point.x * 0.5), VerticalSpacing * (1.75 + (point.y - 1) / 2 * 1.5 + vOffset) - VillageSize * 2, VillageSize, GamePieceColours[playerColour]);
            }
        }

        private void OnRoadBuilt(Models.Point point1, Models.Point point2, PlayerColour playerColour)
        {
            double x, y;
            (x, y) = CalculateRoadCoordinates(point1.x, point2.x, point1.y, point2.y);
            DrawRoad(x, y, RoadSize, GamePieceColours[playerColour]);
        }

        private void OnThiefMoved(Models.Point point)
        {
            double rowOffset;
            double oddOffset = HorizontalSpacing / 2;
            if (point.y == 2 || point.y == 3)
            {
                rowOffset = 1;
            }
            else if (point.y == 4)
            {
                rowOffset = 2;
            }
            else
            {
                rowOffset = 0;
            }
            if (point.y % 2 == 0)
            {
                DrawThiefIndicator(HorizontalSpacing * (point.x + rowOffset), VerticalSpacing * (1 + point.y * 0.75));
            }
            else
            {
                DrawThiefIndicator(HorizontalSpacing * (point.x + rowOffset) + oddOffset, VerticalSpacing * (1 + point.y * 0.75));
            }
        }

        private void OnGameStateUpdated(string update)
        {
            if (update.StartsWith("SetHexColorAndNumber:"))
            {
                var parts = update.Split(':')[1].Split(',');
                int x = int.Parse(parts[0]);
                int y = int.Parse(parts[1]);
                string hexcode = parts[2];
                int number = int.Parse(parts[3]);
                SetHexColorAndNumber(x, y, hexcode, number);
            }
            else
            {
                Dispatcher.Invoke(() => GameIdText.Text = update);
            }
        }


        //================================ BOARD DRAWING LOGIC =======================================

        private void DrawBoard()
        {
            if(!isDrawingEnabled) { return; }
            GameBoardCanvas.Children.Clear();

            var hexLayout = new List<(int x, int y)[]>
            {
                new[] { (2, 0), (3, 0), (4, 0) },
                new[] { (1, 1), (2, 1), (3, 1), (4, 1) },
                new[] { (0, 2), (1, 2), (2, 2), (3, 2), (4, 2) },
                new[] { (0, 3), (1, 3), (2, 3), (3, 3) },
                new[] { (0, 4), (1, 4), (2, 4) }
            };

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var row in hexLayout)
            {
                foreach (var (x, y) in row)
                {
                    double centerX = x * HorizontalSpacing + (y % 2 == 1 ? HorizontalSpacing / 2 : 0);
                    double centerY = y * VerticalSpacing * 0.75;
                    minX = Math.Min(minX, centerX - HexSize);
                    maxX = Math.Max(maxX, centerX + HexSize);
                    minY = Math.Min(minY, centerY - HexSize);
                    maxY = Math.Max(maxY, centerY + HexSize);
                }
            }

            double offsetX = (GameBoardCanvas.Width - (maxX - minX)) / 2 - minX;
            double offsetY = (GameBoardCanvas.Height - (maxY - minY)) / 2 - minY;

            foreach (var row in hexLayout)
            {
                foreach (var (x, y) in row)
                {
                    DrawTile(x, y, offsetX, offsetY);
                }
            }
        }

        private void DrawTile(int x, int y, double offsetX, double offsetY)
        {
            if (!isDrawingEnabled) { return; }
            double centerX = (y == 2 || y == 3 ? x + 1 : (y == 4 ? x + 2 : x)) * HorizontalSpacing + (y % 2 == 1 ? HorizontalSpacing / 2 : 0);
            double centerY = y * VerticalSpacing * 0.75 + offsetY;

            Polygon tile = new Polygon
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Gray
            };

            PointCollection points = new PointCollection();
            for (int i = 0; i < 6; i++)
            {
                double angle = 2 * Math.PI / 6 * (i + 0.5);
                double pointX = centerX + HexSize * Math.Cos(angle);
                double pointY = centerY + HexSize * Math.Sin(angle);
                points.Add(new System.Windows.Point(pointX, pointY));
            }
            tile.Points = points;

            TextBlock numberText = new TextBlock
            {
                Text = "",
                FontSize = 40,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            numberText.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 0,          
                ShadowDepth = 0,        
                BlurRadius = 5,         
                Opacity = 1                
            };

            Canvas.SetLeft(numberText, centerX - 14);
            Canvas.SetTop(numberText, centerY - 30);
            GameBoardCanvas.Children.Add(numberText);

            GameBoardCanvas.Children.Add(tile);
            Canvas.SetZIndex(tile, 0);
            Canvas.SetZIndex(numberText, 1);

            boardTiles[(x, y)] = (tile, numberText, Brushes.Gray, 0);
        }

        private Polygon DrawVillage(double centerX, double centerY, double size, Brush fillColor)
        {
            if (!isDrawingEnabled) { return null; }
            Polygon village = new Polygon
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = fillColor
            };

            PointCollection points = new PointCollection
            {
                new System.Windows.Point(centerX - size / 2, centerY + size / 3), // Bottom-left
                new System.Windows.Point(centerX + size / 2, centerY + size / 3), // Bottom-right
                new System.Windows.Point(centerX + size / 2, centerY - size / 2), // Top-right
                new System.Windows.Point(centerX, centerY - size),                // Apex
                new System.Windows.Point(centerX - size / 2, centerY - size / 2)  // Top-left
            };
            village.Points = points;

            GameBoardCanvas.Children.Add(village);
            Canvas.SetZIndex(village, 2); // Ensure villages/towns are above hexes and numbers

            return village;
        }

        private Polygon DrawTown(double centerX, double centerY, double size, Brush fillColor)
        {
            if (!isDrawingEnabled) { return null; }
            Polygon town = new Polygon
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = fillColor
            };

            PointCollection points = new PointCollection
            {
                new System.Windows.Point(centerX - size / 2, centerY + size * 1.5),         // Bottom-left of larger base
                new System.Windows.Point(centerX + size * 1.2, centerY + size * 1.5),         // Bottom-right of larger base
                new System.Windows.Point(centerX + size * 1.2, centerY + size / 2),     // Top-right of larger base
                new System.Windows.Point(centerX + size / 2, centerY + size / 2), // Bottom-right of upper rectangle
                new System.Windows.Point(centerX + size / 2, centerY - size / 6), // Top-right of upper rectangle
                new System.Windows.Point(centerX, centerY - size / 2),                // Apex
                new System.Windows.Point(centerX - size / 2, centerY - size / 6), // Top-left of upper rectangle
                new System.Windows.Point(centerX - size / 2, centerY + size / 2), // Bottom-left of upper rectangle
                new System.Windows.Point(centerX - size / 2, centerY + size / 2)      // Top-left of larger base
            };
            town.Points = points;

            GameBoardCanvas.Children.Add(town);
            Canvas.SetZIndex(town, 2); // Ensure villages/towns are above hexes and numbers

            return town;
        }

        private Ellipse DrawRoad(double centerX, double centerY, double size, Brush fillColor)
        {
            if (!isDrawingEnabled) { return null; }

            Ellipse road = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fillColor,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            // Position the ellipse so its center is at (centerX, centerY)
            Canvas.SetLeft(road, centerX - size / 2);
            Canvas.SetTop(road, centerY - size / 1.25);

            GameBoardCanvas.Children.Add(road);
            Canvas.SetZIndex(road, 2);

            return road;
        }

        private (double, double) CalculateRoadCoordinates(int x1, int x2, int y1, int y2)
        {
            if (!isDrawingEnabled) { return (0, 0); }
            double vOffset = 0;
            double h1, h2;
            double v1, v2;
            if (y1 % 2 == 0)
            {
                if (x1 % 2 == 0)
                {
                    vOffset = 0.25;
                }
                else
                {
                    vOffset = 0;
                }
                //DrawVillage(HorizontalSpacing * (0.5 + x1 * 0.5), VerticalSpacing * (0.75 + y1 / 2 * 1.5 + vOffset) - VillageSize, VillageSize, Brushes.LimeGreen);
                h1 = HorizontalSpacing * (0.5 + x1 * 0.5);
                v1 = VerticalSpacing * (0.75 + y1 / 2 * 1.5 + vOffset) - VillageSize;
            }
            else
            {
                if (x1 % 2 == 0)
                {
                    vOffset = -0.25;
                }
                else
                {
                    vOffset = 0;
                }
                //DrawVillage(HorizontalSpacing * (0.5 + x1 * 0.5), VerticalSpacing * (1.75 + (y1 - 1) / 2 * 1.5 + vOffset) - VillageSize, VillageSize, Brushes.LimeGreen);
                h1 = HorizontalSpacing * (0.5 + x1 * 0.5);
                v1 = VerticalSpacing * (1.75 + (y1 - 1) / 2 * 1.5 + vOffset) - VillageSize;
            }
            if (y2 % 2 == 0)
            {
                if (x2 % 2 == 0)
                {
                    vOffset = 0.25;
                }
                else
                {
                    vOffset = 0;
                }
                //DrawVillage(HorizontalSpacing * (0.5 + x2 * 0.5), VerticalSpacing * (0.75 + y2 / 2 * 1.5 + vOffset) - VillageSize, VillageSize, Brushes.LimeGreen);
                h2 = HorizontalSpacing * (0.5 + x2 * 0.5);
                v2 = VerticalSpacing * (0.75 + y2 / 2 * 1.5 + vOffset) - VillageSize;
            }
            else
            {
                if (x2 % 2 == 0)
                {
                    vOffset = -0.25;
                }
                else
                {
                    vOffset = 0;
                }
                //DrawVillage(HorizontalSpacing * (0.5 + x2 * 0.5), VerticalSpacing * (1.75 + (y2 - 1) / 2 * 1.5 + vOffset) - VillageSize, VillageSize, Brushes.LimeGreen);
                h2 = HorizontalSpacing * (0.5 + x2 * 0.5);
                v2 = VerticalSpacing * (1.75 + (y2 - 1) / 2 * 1.5 + vOffset) - VillageSize;
            }

            double x = (h1 + h2) / 2;
            double y = (v1 + v2) / 2;
            return (x, y);
        }

        private void DrawThiefIndicator(double centerX, double centerY)
        {
            if (!isDrawingEnabled) { return; }
            // Remove the old thief indicator if it exists
            if (thiefIndicator != null)
            {
                GameBoardCanvas.Children.Remove(thiefIndicator);
            }

            // Define the size of the thief indicator (relative to HexSize)
            double thiefSize = HexSize; // Adjust as needed (e.g., HexSize / 3 for a smaller ellipse)

            // Create the new thief indicator (dark gray ellipse)
            thiefIndicator = new Ellipse
            {
                Width = thiefSize,
                Height = thiefSize,
                Fill = HexToBrush("#25262b"),
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            // Position the ellipse so its center is at (centerX, centerY)
            Canvas.SetLeft(thiefIndicator, centerX - thiefSize / 2);
            Canvas.SetTop(thiefIndicator, centerY - thiefSize / 2);

            // Add the thief to the canvas
            GameBoardCanvas.Children.Add(thiefIndicator);
            Canvas.SetZIndex(thiefIndicator, 3); // Ensure the thief is above hexes (z=0), numbers (z=1), and buildings (z=2)
        }

        private SolidColorBrush HexToBrush(string hexColor) => (SolidColorBrush)new BrushConverter().ConvertFrom(hexColor);

        public void SetHexColorAndNumber(int x, int y, string color, int number)
        {
            if (!isDrawingEnabled) { return; }
            if (boardTiles.TryGetValue((x, y), out var element))
            {
                element.hex.Fill = HexToBrush(color);
                element.numberText.Text = number.ToString();
                boardTiles[(x, y)] = (element.hex, element.numberText, HexToBrush(color), number);
            }
        }

    }
}

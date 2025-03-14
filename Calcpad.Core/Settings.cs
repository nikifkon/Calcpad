﻿namespace Calcpad.Core
{
    public class Settings
    {
        public MathSettings Math { get; set; }
        public PlotSettings Plot { get; set; }
        public string Units { get; set; }
        public Settings()
        {
            Math = new MathSettings();
            Plot = new PlotSettings();
            Units = "m";
        }
    }

    public class MathSettings
    {
        private int _decimals;
        public int Decimals
        {
            get => _decimals;
            set
            {
                _decimals = value switch
                {
                    <= 0 => 0,
                    >= 15 => 15,
                    _ => value
                };
            }
        }
        public bool Degrees { get; set; }
        public bool IsComplex { get; set; }
        public bool Substitute { get; set; }
        public bool FormatEquations { get; set; }

        public MathSettings()
        {
            Decimals = 2;
            Degrees = true;
            IsComplex = false;
            Substitute = true;
            FormatEquations = true;
        }
    }

    public class PlotSettings
    {
        private bool _shadows;
        public bool IsAdaptive { get; set; }
        public double ScreenScaleFactor { get; set; } = 1.0;
        public string ImagePath { get; set; }
        public string ImageUri { get; set; }
        public bool VectorGraphics { get; set; }
        public ColorScales ColorScale { get; set; }
        public bool SmoothScale { get; set; }
        public bool Shadows
        {
            set => _shadows = value;
            get => _shadows && ColorScale != ColorScales.Gray || ColorScale == ColorScales.None;
        }
        public LightDirections LightDirection { get; set; }

        public enum LightDirections
        {
            North,
            NorthEast,
            East,
            SouthEast,
            South,
            SouthWest,
            West,
            NorthWest
        }

        public enum ColorScales
        {
            None,
            Gray,
            Rainbow,
            Terrain,
            VioletToYellow,
            GreenToYellow,
            Blues
        }

        public PlotSettings()
        {
            IsAdaptive = true;
            ImagePath = string.Empty;
            ImageUri = string.Empty;
            VectorGraphics = false;
            ColorScale = ColorScales.Rainbow;
            SmoothScale = false;
            Shadows = true;
            LightDirection = LightDirections.NorthWest;
        }
    }
}
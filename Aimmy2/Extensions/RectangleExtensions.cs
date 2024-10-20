﻿using System.Drawing;
using System.Windows;
using Point = System.Drawing.Point;

namespace Aimmy2.Extensions;

public static class RectangleExtensions
{
    public static System.Windows.Point ToPoint(this Point point) => new System.Windows.Point(point.X, point.Y);
    public static Point ToPoint(this System.Windows.Point point) => new Point((int)point.X, (int)point.Y);

    public static int GetLeft(this Rectangle rect) => rect.X;
    public static int GetTop(this Rectangle rect) => rect.Y;
    public static int GetRight(this Rectangle rect) => rect.X + rect.Width;
    public static int GetBottom(this Rectangle rect) => rect.Y + rect.Height;
    public static Point GetBottomCenter(this Rectangle rect) => new Point(rect.X + rect.Width / 2, rect.Y + rect.Height);
    public static Point GetTopCenter(this Rectangle rect) => new Point(rect.X + rect.Width / 2, rect.Y);
    public static Point GetLeftCenter(this Rectangle rect) => new Point(rect.X, rect.Y + rect.Height / 2);
    public static Point GetRightCenter(this Rectangle rect) => new Point(rect.X + rect.Width, rect.Y + rect.Height / 2);
    public static Point GetCenter(this Rectangle rect) => new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    public static RectangleF ToRectangleF(this Rectangle rect) => new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);

    public static float GetLeft(this RectangleF rect) => rect.X;
    public static float GetTop(this RectangleF rect) => rect.Y;
    public static float GetRight(this RectangleF rect) => rect.X + rect.Width;
    public static float GetBottom(this RectangleF rect) => rect.Y + rect.Height;
    public static PointF GetBottomCenter(this RectangleF rect) => new PointF(rect.X + rect.Width / 2, rect.Y + rect.Height);
    public static PointF GetTopCenter(this RectangleF rect) => new PointF(rect.X + rect.Width / 2, rect.Y);
    public static PointF GetLeftCenter(this RectangleF rect) => new PointF(rect.X, rect.Y + rect.Height / 2);
    public static PointF GetRightCenter(this RectangleF rect) => new PointF(rect.X + rect.Width, rect.Y + rect.Height / 2);
    public static PointF GetCenter(this RectangleF rect) => new PointF(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    public static Rectangle ToRectangle(this RectangleF rect) => new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
    public static Rect ToRect(this RectangleF rect) => new Rect(rect.X, rect.Y, rect.Width, rect.Height);
    public static Rect ToRect(this Rectangle rect) => new Rect(rect.X, rect.Y, rect.Width, rect.Height);



    public static double GetLeft(this Rect rect) => rect.X;
    public static double GetTop(this Rect rect) => rect.Y;
    public static double GetRight(this Rect rect) => rect.X + rect.Width;
    public static double GetBottom(this Rect rect) => rect.Y + rect.Height;
    public static PointF GetBottomCenter(this Rect rect) => new PointF((float)(rect.X + rect.Width / 2), (float)(rect.Y + rect.Height));
    public static PointF GetTopCenter(this Rect rect) => new PointF((float)(rect.X + rect.Width / 2), (float)rect.Y);
    public static PointF GetLeftCenter(this Rect rect) => new PointF((float)rect.X, (float)(rect.Y + rect.Height / 2));
    public static PointF GetRightCenter(this Rect rect) => new PointF((float)(rect.X + rect.Width), (float)(rect.Y + rect.Height / 2));
    public static PointF GetCenter(this Rect rect) => new PointF((float)(rect.X + rect.Width / 2), (float)(rect.Y + rect.Height / 2));
    public static Rectangle ToRectangle(this Rect rect) => new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
    public static RectangleF ToRectangleF(this Rect rect) => new RectangleF((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);



}
﻿namespace klooie;

/// <summary>
/// Provides math helpers that are relevant for the console
/// </summary>
public static class ConsoleMath
{
    /// <summary>
    /// In most consoles the recrtangles allocated to characters are about twice as tall as they
    /// are wide. Since we want to treat the console like a uniform grid we'll have to account for that.
    /// 
    /// This method takes in some quantity and an angle and normalizes it so that if the angle were flat (e.g. 0 or 180)
    /// then you'll get back the same quantity you gave in. If the angle is vertical (e.g. 90 or 270) then you will get back
    /// a quantity that is only half of what you gave. The degree to which we normalize the quantity is linear.
    /// </summary>
    /// <param name="quantity">The quantity to normalize</param>
    /// <param name="angle">the angle to use to adjust the quantity</param>
    /// <param name="reverse">if true, grows the quantity instead of shrinking it. This is useful for angle quantities.</param>
    /// <returns>the normalized quantity</returns>
    public static float NormalizeQuantity(this int quantity, Angle angle, bool reverse = false) => NormalizeQuantity((float)quantity, angle, reverse);
    
    /// <summary>
    /// Rounds using normal rounding, not banker's rounding
    /// </summary>
    /// <param name="f">the # to round</param>
    /// <param name="digits">the number of digits to round to</param>
    /// <returns>the rounded number</returns>
    public static float Round(float f, int digits) => (float)Math.Round(f, digits, MidpointRounding.AwayFromZero);
    
    /// <summary>
    /// Rounds using normal rounding, not banker's rounding
    /// </summary>
    /// <param name="f">the # to round</param>
    /// <param name="digits">the number of digits to round to</param>
    /// <returns>the rounded number</returns>
    public static float Round(double d, int digits) => (float)Math.Round(d, digits, MidpointRounding.AwayFromZero);
    
    /// <summary>
    /// Rounds to an int using normal rounding, not banker's rounding
    /// </summary>
    /// <param name="f">the number to round</param>
    /// <returns>the rounded number</returns>
    public static int Round(float f) => (int)Math.Round(f, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Rounds to an int using normal rounding, not banker's rounding
    /// </summary>
    /// <param name="f">the number to round</param>
    /// <returns>the rounded number</returns>
    public static int Round(double d) => (int)Math.Round(d, MidpointRounding.AwayFromZero);


    /// <summary>
    /// In most consoles the recrtangles allocated to characters are about twice as tall as they
    /// are wide. Since we want to treat the console like a uniform grid we'll have to account for that.
    /// 
    /// This method takes in some quantity and an angle and normalizes it so that if the angle were flat (e.g. 0 or 180)
    /// then you'll get back the same quantity you gave in. If the angle is vertical (e.g. 90 or 270) then you will get back
    /// a quantity that is only half of what you gave. The degree to which we normalize the quantity is linear.
    /// </summary>
    /// <param name="quantity">The quantity to normalize</param>
    /// <param name="angle">the angle to use to adjust the quantity</param>
    /// <param name="reverse">if true, grows the quantity instead of shrinking it. This is useful for angle quantities.</param>
    /// <returns>the normalized quantity</returns>
    public static float NormalizeQuantity(this float quantity, Angle angle, bool reverse = false)
    {
        var degreesFromFlat = angle.Value <= 180 ? Math.Min(180 - angle.Value, angle.Value) : Math.Min(angle.Value - 180, 360 - angle.Value);
        var skewPercentage = 1 + (degreesFromFlat / 90);
        return reverse ? quantity * skewPercentage : quantity / skewPercentage;
    }
}


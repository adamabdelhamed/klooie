using System.Reflection;
namespace klooie;


public sealed class SliderGenerator : FormFieldGeneratorAttribute
{
    public override FormElement Generate(PropertyInfo property, IObservableObject formModel)
    {
        var sliderAttr = property.Attr<FormSliderAttribute>();
        var slider = new SliderWithValueLabel()
        {
            Min = sliderAttr.Min,
            Max = sliderAttr.Max,
            Increment = sliderAttr.Increment
        };
        SyncSlider(property, slider, formModel);
        return new FormElement()
        {
            Label = GenerateLabel(property, formModel),
            ValueControl = slider,
        };
    }

    public override float GetConfidence(PropertyInfo property, IObservableObject formModel)
    {
        var sliderAttr = property.Attr<FormSliderAttribute>();
        if (sliderAttr == null) return 0;
        AssertSupportedType(property, sliderAttr);
        return 1;
    }

    private void AssertSupportedType(PropertyInfo property, FormSliderAttribute sliderAttr)
    {
        if (property.PropertyType is float) return;
        
        try
        {
            var minAsPropertyValue = Convert.ChangeType(sliderAttr.Min, property.PropertyType);
            var maxAsPropertyValue = Convert.ChangeType(sliderAttr.Max, property.PropertyType);

            var minConvertedBackToFloat = Convert.ChangeType(minAsPropertyValue, typeof(float));
            var maxConvertedBackToFloat = Convert.ChangeType(maxAsPropertyValue, typeof(float));

            var minConvertSuccess = minConvertedBackToFloat.Equals(sliderAttr.Min);
            var maxConvertSuccess = maxConvertedBackToFloat.Equals(sliderAttr.Max);

            if (minConvertSuccess == false || maxConvertSuccess == false)
            {
                throw new NotSupportedException($"{nameof(FormSliderAttribute)} is not supported for property {property.Name} of type {property.PropertyType.Name} because the values could not be converted to a float in the correct range");
            }
        }
        catch (Exception ex)
        {
            throw new NotSupportedException($"{nameof(FormSliderAttribute)} is not supported for property {property.Name} of type {property.PropertyType.Name} because it could not be converted to a float");
        }
    }

    protected void SyncSlider(PropertyInfo property, SliderWithValueLabel slider, IObservableObject formModel)
    {
        formModel.Sync(property.Name, () => slider.Value = (float)Convert.ChangeType(property.GetValue(formModel), typeof(float)), slider);
        slider.Subscribe(nameof(slider.Value), () => property.SetValue(formModel, Convert.ChangeType(slider.Value, property.PropertyType)) , slider);
    }
}

public sealed class FormSliderAttribute : Attribute
{
    public RGB BarColor { get; set; } = RGB.White;
    public RGB HandleColor { get; set; } = RGB.Gray;
    public float Min { get; set; } = 0;
    public float Max { get; set; } = 100;
    public float Increment { get; set; } = 10;
    public bool EnableWAndSKeysForUpDown { get; set; } = false;

    public Slider Factory()
    {
        return new Slider()
        {
            BarColor = BarColor,
            HandleColor = HandleColor,
            Min = Min,
            Max = Max,
            Increment = Increment,
            EnableWAndSKeysForUpDown = EnableWAndSKeysForUpDown
        };
    }
}
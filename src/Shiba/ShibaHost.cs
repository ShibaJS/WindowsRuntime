using Windows.UI.Xaml;
using Shiba.Controls;
using Shiba.Internal;
using Shiba.Visitors;
using NativeParent = Windows.UI.Xaml.Controls.ContentControl;


namespace Shiba
{
    //[ContentProperty(Name = nameof(Layout))]
    public class ShibaHost : NativeParent, IShibaHost
    {
        public static readonly DependencyProperty ComponentProperty = DependencyProperty.Register(
            nameof(Component), typeof(string), typeof(ShibaHost),
            new PropertyMetadata(default, PropertyChangedCallback));

        private string _creator;

        public ShibaHost()
        {
            Context = new ShibaContext
            {
                ShibaHost = this
            };
        }

        public string Component
        {
            get => (string) GetValue(ComponentProperty);
            set => SetValue(ComponentProperty, value);
        }

        public string Creator
        {
            get => _creator;
            set
            {
                _creator = value;
                OnCreatorChanged(value);
            }
        }

        private void OnCreatorChanged(string value)
        {
            Content = NativeRenderer.RenderFromFunction(value, Context);
        }

        public IShibaContext Context { get; }

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == ComponentProperty)
            {
                (d as ShibaHost).OnComponentChanged(e.NewValue as string);
            }
        }

        private void OnComponentChanged(string newValue)
        {
            if (ShibaApp.Instance.Components.TryGetValue(newValue, out var component))
            {
                Content = NativeRenderer.Render(component, Context);
            }
        }
    }
}
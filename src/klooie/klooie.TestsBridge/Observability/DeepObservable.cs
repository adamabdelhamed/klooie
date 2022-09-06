namespace ArgsTests
{
    public class ObservableAccount : ObservableObject
    {
        public ObservableCustomer Customer { get { return Get<ObservableCustomer>(); } set { Set(value); } }
    }

    public class ObservableCustomer : ObservableObject
    {
        public BasicInfo BasicInfo { get { return Get<BasicInfo>(); } set { Set(value); } }
    }

    public class BasicInfo : ObservableObject
    {
        public string Name { get { return Get<string>(); } set { Set(value); } }
    }
}

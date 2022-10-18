using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArgsTests.CLI.Controls
{
    [TestClass]
    [TestCategory(Categories.ConsoleApp)]
    public class FormTests
    {
        public TestContext TestContext { get; set; }
 
        public class TestFormViewModel : ObservableObject
        {
            public string Name { get => Get<string>(); set => Set(value); }
            [FormReadOnly]
            public int Age { get; set; } = 33;
            [FormLabel("The Address")]
            public string Address { get => Get<string>(); set => Set(value); }

            public TestFormViewModel()
            {
                Name = "Adam";
                Address = "Somewhere here";
            }
        }
    }
}

namespace FireAnt.Interfaces
{
    public class TestCaseSummary
    {
        public TestResult Result { get; set; }
        public decimal Time { get; set; }
        public string Output { get; set; }

        public TestCaseSummary(TestResult result, decimal time, string output)
        {
            Result = result;
            Time = time;
            Output = output;
        }
    }
}
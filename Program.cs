using CustomAlgo.Demo;

namespace CustomAlgo
{
    /// <summary>
    /// Main entry point for the Kite Range Algo application
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Application entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== CUSTOM ALGO DEMO SELECTOR ===");
            Console.WriteLine("1. Kite Range Algo Demo");
            Console.WriteLine("2. Ninjatrader Tick Data Demo");
            Console.WriteLine("3. Token Test");
            Console.WriteLine("4. Instruments Demo");
            Console.WriteLine("Select demo (1-4): ");
            
            var choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    await CustomAlgoDemo.Main(args);
                    break;
                case "2":
                    NinjatraderTickDataDemo.RunDemo();
                    break;
                case "3":
                    TokenTestOnly.RunDemo();
                    break;
                case "4":
                    InstrumentsDemo.RunDemo();
                    break;
                default:
                    Console.WriteLine("Invalid choice. Running default Kite Range Algo Demo...");
                    await CustomAlgoDemo.Main(args);
                    break;
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
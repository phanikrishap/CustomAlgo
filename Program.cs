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
            // Run the demo application
            await CustomAlgoDemo.Main(args);
        }
    }
}
namespace CommandlineSystem
{
    public interface ICommandlineSystem
    {

        string Name { get; }

        void Run(string[] args);

    }
}
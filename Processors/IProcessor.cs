namespace HadesBoonBot.Processors
{
    interface IProcessor
    {
        void Run(List<Classifiers.ClassifiedScreenMeta> screens);
    }
}

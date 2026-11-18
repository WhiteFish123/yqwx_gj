//A persistent noise source (e.g. a burning chest). Enemies that investigate it
//will stay put beside it while IsNoiseActive is true.
public interface INoiseSource
{
    //true while the noise is still being emitted (e.g. chest still burning)
    bool IsNoiseActive { get; }

    //true while the source object still exists. Interface references do not use
    //UnityEngine.Object's fake-null check, so a destroyed source would otherwise
    //keep reporting IsNoiseActive and pin the enemy in place forever.
    bool IsAlive { get; }
}

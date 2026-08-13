namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 풀 재사용 계약. 풀에서 꺼낼 때/반납할 때 상태를 초기화해 누수를 막는다.
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}

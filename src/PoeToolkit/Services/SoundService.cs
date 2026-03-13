namespace PoeCurrencySpammer.Services;

public class SoundService
{
    public void PlayMatchAlert(int frequency, int durationMs)
    {
        Task.Run(() =>
        {
            try
            {
                Console.Beep(frequency, durationMs);
                Thread.Sleep(200);
                Console.Beep((int)(frequency * 1.2), durationMs / 2);
            }
            catch { /* ignore sound errors */ }
        });
    }
}

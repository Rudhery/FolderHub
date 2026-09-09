namespace FolderHub.Services;

/// <summary>
/// A matemática da grade, separada da janela. Era o miolo do MainWindow e não
/// dava para testar sem abrir uma janela de verdade; aqui é função pura.
/// </summary>
public static class GridLayout
{
    public const int MinColumns = 3;

    /// <summary>
    /// Escolhe o número de colunas perto do formato quadrado, mas preferindo
    /// grades em que a última linha fica cheia — 12 atalhos viram 4×3, e não
    /// 5+5+2 com dois buracos no fim.
    /// </summary>
    public static int Columns(int count, int maxColumns)
    {
        int max = Math.Max(MinColumns, maxColumns);
        if (count <= 0) return MinColumns;

        int ideal = Math.Clamp((int)Math.Ceiling(Math.Sqrt(count * 1.6)), MinColumns, max);

        int best = ideal;
        int bestScore = int.MaxValue;

        for (int columns = Math.Max(MinColumns, ideal - 1); columns <= Math.Min(max, ideal + 1); columns++)
        {
            int remainder = count % columns;
            int holes = remainder == 0 ? 0 : columns - remainder;

            // Buraco na última linha pesa o dobro de sair do formato ideal.
            int score = holes * 2 + Math.Abs(columns - ideal);

            if (score < bestScore)
            {
                bestScore = score;
                best = columns;
            }
        }

        return best;
    }

    public static int Rows(int count, int columns)
    {
        if (count <= 0 || columns <= 0) return 1;
        return (int)Math.Ceiling(count / (double)columns);
    }
}

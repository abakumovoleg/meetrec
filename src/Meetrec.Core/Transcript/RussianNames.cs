namespace Meetrec.Core.Transcript;

public static class RussianNames
{
    private static readonly Dictionary<string, string> Names = BuildIndex(
        """
        Александр Саша Саня Шура Алексей Лёша Лёха Анатолий Толя Андрей Андрюша Антон Тоша Аркадий Аркаша
        Артём Тёма Артур Борис Боря Вадим Валентин Валя Валерий Валера Василий Вася Виктор Витя Виталий
        Владимир Володя Вова Владислав Влад Слава Всеволод Сева Вячеслав Георгий Гоша Жора Глеб Григорий Гриша Давид
        Даниил Данила Даня Денис Дмитрий Дима Евгений Женя Егор Иван Ваня Игорь Илья Иннокентий Кеша Кирилл Киря
        Константин Костя Лев Лёва Леонид Лёня Максим Макс Марк Матвей Михаил Миша Никита Николай Коля Олег
        Павел Паша Пётр Петя Роман Рома Руслан Семён Сеня Сергей Серёжа Серёга Станислав Стас
        Степан Стёпа Тимофей Тимур Тимоха Фёдор Федя Филипп Эдуард Эдик Юрий Юра Ярослав Ярик Эмиль Ринат
        Рустам Айдар Азат Камиль Ильдар Тагир Марат Алик
        Александра Алёна Алина Алиса Алла Анастасия Настя Ангелина Анна Аня Анюта Антонина Тоня Валентина Вера
        Вероника Ника Виктория Вика Галина Галя Дарья Даша Диана Ева Евгения Екатерина Катя Елена Лена Елизавета Лиза
        Жанна Зинаида Зина Зоя Инна Ирина Ира Карина Кира Кристина Ксения Ксюша Лариса Лидия Лида Люба Людмила
        Люда Мила Маргарита Рита Марина Мария Маша Милана Надежда Надя Наталья Наталия Наташа Нина Оксана Ольга Оля
        Полина Поля Раиса Регина Светлана Света Снежана Софья София Соня Таисия Тамара Татьяна Таня Ульяна Эвелина
        Элина Эльвира Юлия Юля Яна Ярослава Василиса Варвара Варя Злата Камила Лилия Лиля Дина Гуля
        """);

    // Returns the dictionary form, also for the colloquial vocative: "Ром" → "Рома", "Тань" → "Таня".
    public static string? Find(string word)
    {
        if (Names.TryGetValue(Normalize(word), out var name))
        {
            return name;
        }

        var stem = word.TrimEnd('ь', 'Ь');
        if (stem.Length < 2)
        {
            return null;
        }

        return Names.TryGetValue(Normalize(stem + "а"), out name) || Names.TryGetValue(Normalize(stem + "я"), out name) ? name : null;
    }

    private static Dictionary<string, string> BuildIndex(string names)
    {
        var index = new Dictionary<string, string>();
        foreach (var name in names.Split((char[])[' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            index.TryAdd(Normalize(name), name);
        }

        return index;
    }

    private static string Normalize(string word) => word.ToLowerInvariant().Replace('ё', 'е');
}

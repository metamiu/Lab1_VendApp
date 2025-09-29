using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VendApp
{
    internal class Program // стартовый наборчик 
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var inventory = new Inventory();
            // cтартовый набор 
            inventory.AddProduct(new Product("A1", "Трусы", 7, 7));
            inventory.AddProduct(new Product("A2", "Носки", 5, 3));
            inventory.AddProduct(new Product("A3", "Перчатки", 31, 2));

            var wallet  = new SessionWallet();
            var machine = new VendingMachine(inventory, wallet);

            CommandProcessor.Run(machine);
        }
    }


    public static class CommandProcessor // цикл менюшки 
    {
        public static void Run(VendingMachine machine)
        {
            while (true)
            {
                ConsoleUI.PrintMainMenu();
                var cmd = ConsoleUI.ReadTrimmed();
                switch (cmd)
                {
                    case "SH": // показать товары
                        machine.ShowProducts();
                        break;

                    case "I": // внесите монетку
                        machine.InsertCoinFlow();
                        break;

                    case "CH": // // выбрать товар 
                        machine.ChooseProductFlow();
                        break;

                    case "C": // // отмена покупки
                        machine.CancelPurchase();
                        break;

                    case "A": // // вход в админ-панель (пароль POPA)
                        machine.AdminPanel();
                        break;

                    default: // ввели что то другое 
                        Console.WriteLine("Неизвестная команда. Используйте [SH], [I], [CH], [C], [A].");
                        break;
                }
            }
        }
    }


    public class VendingMachine // основная логика автомата 
    {
        private readonly Inventory _inventory; // хранилище с товарами 
        private readonly SessionWallet _wallet; // кошелек текущей ссесии 
        public int Profit { get; private set; } // накопленный банк автомата

        public VendingMachine(Inventory inventory, SessionWallet wallet)
        {
            _inventory = inventory;
            _wallet = wallet;
            Profit = 0;
        }

        public void ShowProducts() // для вывода списка товаров 
        {
            Console.WriteLine("список доступных товаров:");
            foreach (var p in _inventory.GetAll())
            {
                Console.WriteLine($"Код: {p.Code}  Название: {p.Name}  {p.Price} р.  Доступно: {p.Quantity} шт.");
            }
        }

        public void InsertCoinFlow() // внесение денег 
        {
            // успешный приём одной монеты, если два неверных ввода вернёт кошелек сессии 
            Console.WriteLine("Внесите монету номиналом 10, 5, 2, 1:");
            if (!TryReadValidCoin(out int coin))
            {
                Console.WriteLine("Нельзя внести такую сумму. Введите другой номинал:");
                if (!TryReadValidCoin(out coin))
                {
                    Console.WriteLine($"Вы ввели не тот номинал, ваша сдача: {_wallet.Balance} р");
                    _wallet.Reset(); // обнуляем кошелёк тк ввел два раза не тот номинал 
                    return;
                }
            }

            _wallet.Add(coin); // если всё ок то добавляем к балансу
            Console.WriteLine($"Ваш баланс сессии: {_wallet.Balance} р");
        }

        public void ChooseProductFlow() // выбор товара
        {
            while (true)
            {
                Console.WriteLine("Введите код товара:");
                var code = ConsoleUI.ReadTrimmed();

                if (!_inventory.TryGet(code, out var product))
                {
                    Console.WriteLine("Товар с таким кодом не найден. Повторите ввод кода.");
                    continue; // остаёмся в введите код товара
                }

                if (product.Quantity <= 0) // если товара 0 шт
                {
                    if (!AskYesNo("Товар недоступен. Продолжить покупку y/n?"))
                    {
                        RefundAndToMenu();
                        return;
                    }
                    continue; // снова введите код товара
                }

                Console.WriteLine($"Ваш баланс {_wallet.Balance} р");

                // хватает ли средств проверяем 
                if (_wallet.Balance < product.Price)
                {
                    var proceed = AskYesNo("Недостаточно средств. Продолжить внесение средств y/n?");
                    if (!proceed)
                    {
                        RefundAndToMenu(); 
                        return;
                    }

                    // внесение монет до достижения цены
                    if (!InsertCoinsUntilAtLeast(product.Price))
                    {
                        return;
                    }
                }

                // денег достаточно
                if (!AskYesNo("Подтверждаем покупку y/n?"))
                {
                    RefundAndToMenu();
                    return;
                }

                if (product.Quantity <= 0)
                {
                    if (!AskYesNo("Товар недоступен. Продолжить покупку y/n?"))
                    {
                        RefundAndToMenu();
                        return;
                    }
                    continue;
                }

                // выдача
                CompleteSale(product);
                return; // после успешной покупки возвращаемся менюшку обратно 
            }
        }

        public void CancelPurchase() // отмена!!
        {
            if (_wallet.Balance <= 0)
            {
                Console.WriteLine("Вы не ввели монеты и не выбрали товар, отмена не может быть совершена!");
                return;
            }
            Console.WriteLine($"Отмена успешно прошла! Ваша сдача: {_wallet.Balance} р");
            _wallet.Reset();
        }

        public void AdminPanel() // админка - 3 попытки ввода пароля
        {
            const string password = "POPA";
            bool ok = false;
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine("Здравствуй хозяин! Введи пароль:");
                var attempt = ConsoleUI.ReadTrimmed();
                if (attempt == password)
                {
                    ok = true;
                    break;
                }
                Console.WriteLine("Неверный пароль.");
            }

            if (!ok)
            {
                Console.WriteLine("Упс! Вы не админ(");
                return;
            }

            while (true) // меню админки
            {
                ConsoleUI.PrintAdminMenu();
                var cmd = ConsoleUI.ReadTrimmed();

                switch (cmd)
                {
                    case "P": // добавление 
                        AdminReplenish();
                        Console.WriteLine("Вот весь ассортимент:");
                        foreach (var p in _inventory.GetAll())
                            Console.WriteLine($"Код: {p.Code}  Название: {p.Name}  {p.Price} р.  Доступно: {p.Quantity} шт.");
                        break;

                    case "N": // забрать выручку
                        Console.WriteLine($"Ваша выручка: {Profit} р");
                        Profit = 0;
                        Console.WriteLine("Текущий баланс автомата: 0 р");
                        break;

                    case "E": // выход
                        Console.WriteLine("До свидания!");
                        return;

                    default:
                        Console.WriteLine("Неизвестная команда. Доступно: [P], [N], [E].");
                        break;
                }
            }
        }


        private bool TryReadValidCoin(out int coin) // проверка доступных номиналов
        {
            coin = 0;
            var input = ConsoleUI.ReadTrimmed();
            if (!int.TryParse(input, out var value))
                return false;

            if (!Money.IsValidCoin(value))
                return false;

            coin = value;
            return true;
        }

        private bool InsertCoinsUntilAtLeast(int requiredAmount) 
        {
            while (_wallet.Balance < requiredAmount)
            {
                Console.WriteLine("Внесите монету номиналом 10, 5, 2, 1:");
                if (!TryReadValidCoin(out int coin))
                {
                    Console.WriteLine("Нельзя внести такую сумму. Введите другой номинал:");
                    if (!TryReadValidCoin(out coin))
                    {
                        Console.WriteLine($"Вы ввели не тот номинал, ваша сдача: {_wallet.Balance} р");
                        _wallet.Reset();
                        return false;
                    }
                }

                _wallet.Add(coin);
                Console.WriteLine($"Ваш баланс сессии: {_wallet.Balance} р");
            }
            return true;
        }

        private bool AskYesNo(string message) // обработка вопроса y/n
        {
            while (true)
            {
                Console.WriteLine(message);
                var answer = ConsoleUI.ReadTrimmed();
                if (answer == "y") return true;
                if (answer == "n") return false;
                Console.WriteLine("Введите 'y' или 'n'.");
            }
        }

        private void RefundAndToMenu() // возвращаем деньги и обнуляем кошелёк
        {
            Console.WriteLine($"Ваша сдача: {_wallet.Balance} р");
            _wallet.Reset();
        }

        private void CompleteSale(Product product) // уменьшаем остаток товара, сдача, прибыль, обнуление баланса сессии
        {
            int price  = product.Price;
            int change = _wallet.Balance - price;

            product.Quantity -= 1;

            // внесено – сдача = наш заработок
            Profit += (_wallet.Balance - change);

            Console.WriteLine($"Ваш товар {product.Code} готов к получению!");
            Console.WriteLine($"Ваша сдача: {change} р Баланс сессии: 0 р");
            Console.WriteLine("Спасибо за покупку!");

            _wallet.Reset();
        }

        private void AdminReplenish() // операция пополнения товара или просто к товару + сколько то 
        {
            bool addNew = AskYesNo("Добавить новый товар y/n?");

            if (addNew)
            {
                string code = ReadUniqueCodeOrErrorLoop();

                Console.WriteLine("Введи имя нового товара:");
                string name = ConsoleUI.ReadNonEmpty();

                Console.WriteLine("Введи цену нового товара:");
                int price = ConsoleUI.ReadPositiveInt();

                Console.WriteLine("Введи количество нового товара:");
                int qty = ConsoleUI.ReadNonNegativeInt();

                _inventory.AddProduct(new Product(code, name, price, qty));
                Console.WriteLine("Товар добавлен!");
            }
            else
            {
                Console.WriteLine("Введите код существующего товара для пополнения:");
                string code = ConsoleUI.ReadTrimmed();

                if (!_inventory.TryGet(code, out var product))
                {
                    Console.WriteLine("Товар с таким кодом не найден.");
                    return;
                }

                Console.WriteLine("Введите количество, на которое пополнить:");
                int delta = ConsoleUI.ReadPositiveInt();
                _inventory.IncreaseQuantity(code, delta);
                Console.WriteLine("Количество пополнено!");
            }
        }

        private string ReadUniqueCodeOrErrorLoop() // уникальный код для каждого товара
        {
            while (true)
            {
                Console.WriteLine("Введи код для нового товара:");
                string code = ConsoleUI.ReadTrimmed();

                if (!Money.IsValidProductCode(code))
                {
                    Console.WriteLine("Код должен быть в формате: заглавная буква + цифры (например, A4, B12).");
                    continue;
                }

                if (_inventory.Exists(code))
                {
                    Console.WriteLine("Товар с таким кодом уже существует, выберите другой код.");
                    continue;
                }

                return code;
            }
        }
    }

   
    public class Inventory // хранилище с товарами
    {
        private readonly Dictionary<string, Product> _byCode = new();
        private readonly List<Product> _ordered = new();

        public bool AddProduct(Product product)
        {
            if (_byCode.ContainsKey(product.Code))
                return false;

            _byCode[product.Code] = product;
            _ordered.Add(product);
            return true;
        }

        public bool TryGet(string code, out Product product)
            => _byCode.TryGetValue(code, out product!);

        public bool Exists(string code) => _byCode.ContainsKey(code);

        public IEnumerable<Product> GetAll() => _ordered;

        public bool IncreaseQuantity(string code, int delta)
        {
            if (!_byCode.TryGetValue(code, out var p)) return false;
            p.Quantity += delta;
            return true;
        }
    }

   
    public class Product // наш класс товара 
    {
        public string Code { get; }
        public string Name { get; }
        public int Price { get; }
        public int Quantity { get; set; }

        public Product(string _code, string _name, int _price, int _quantity)
        {
            Code = _code;
            Name = _name;
            Price = _price;
            Quantity = _quantity;
        }
    }


    public class SessionWallet // кошелек одного покупателя (текущий: сколько закинул пользователь)
    {
        public int Balance { get; private set; }
        public void Add(int coin) => Balance += coin;
        public void Reset() => Balance = 0;
    }

    
    public static class Money 
    {
        private static readonly int[] Allowed = new[] { 10, 5, 2, 1 };
        private static readonly Regex CodeRegex = new(@"^[A-Z][0-9]+$", RegexOptions.Compiled);

        public static bool IsValidCoin(int value)
        {
            foreach (var a in Allowed)
                if (a == value) return true;
            return false;
        }

        // код товара валиден если: заглавная буква + одна или больше цифр
        public static bool IsValidProductCode(string code) => CodeRegex.IsMatch(code);
    }

    public static class ConsoleUI
    {
        public static void PrintMainMenu()
        {
            Console.WriteLine("выберите действие: [SH] - посмотреть список доступных товаров, [I] - внести монету, [CH] - выбрать товар, [C] - отмена покупки, [A] - админская панель");
        }

        public static void PrintAdminMenu()
        {
            Console.WriteLine("Выбери действие: [P] - пополнить ассортимент, [N] - сбор заработанных средств, [E] - покинуть админ панель");
        }

        public static string ReadTrimmed()
        {
            var s = Console.ReadLine() ?? string.Empty;
            return s.Trim();
        }

        public static string ReadNonEmpty()
        {
            while (true)
            {
                var s = ReadTrimmed();
                if (!string.IsNullOrEmpty(s)) return s;
                Console.WriteLine("Значение не может быть пустым. Повторите ввод:");
            }
        }

        public static int ReadPositiveInt()
        {
            while (true)
            {
                var s = ReadTrimmed();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0)
                    return v;
                Console.WriteLine("Введите целое число больше 0:");
            }
        }

        public static int ReadNonNegativeInt()
        {
            while (true)
            {
                var s = ReadTrimmed();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v >= 0)
                    return v;
                Console.WriteLine("Введите целое число 0 или больше:");
            }
        }
    }
} 

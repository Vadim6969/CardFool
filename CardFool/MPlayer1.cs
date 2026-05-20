using System;
using System.Collections.Generic;

namespace CardFool
{
    internal class MPlayer1
    {
        private readonly string name = "Vasya";
        private readonly List<SCard> hand = new List<SCard>();       // карты на руке
        private readonly HashSet<string> seenCards = new HashSet<string>();
        private const int DeckSize = 36;                    // всего карт в игре
        private const int MinSuit = 0;                      // первая масть в enum Suits
        private const int MaxSuit = 3;                      // последняя масть в enum Suits
        private const int MinRank = 6;                      // минимальный ранг карты в колоде
        private const int MaxRank = 14;                     // максимальный ранг карты в колоде, туз
        private const int LowRank = 11;                     // ранг до валета включительно считаем мелкой картой для сброса
        private const int HighRank = 12;                    // с дамы карта считается старшей и ее не сбрасываем без причины
        private const int EndgameHandLimit = 3;             // если у нас карт не больше этого числа, пора играть агрессивнее
        private const int EndgameUnknownLimit = 8;          // если неизвестных карт не больше этого числа, считаем игру близкой к концу
        private const int EarlyTrumpCost = 100;             // добавляется к цене козыря до эндшпиля, чтобы бот берег козыри
        private const int EndgameTrumpCost = 35;            // добавляется к цене козыря в эндшпиле, чтобы бот чаще использовал козыри
        private const int AggressiveTrumpHandLimit = 2;     // если карт больше, козыри не подкидываем даже когда соперник берет
        private const int PairRankDiscount = 2;             // уменьшает цену карты, если в руке есть еще карта такого же ранга
        private const int HighCardCost = 4;                 // добавляется к цене дамы, короля и туза не козырной масти
        private const int HighestRemainingEarlyCost = 2;    // до эндшпиля слегка бережем карту, у которой не видно старших в масти
        private const int HighestRemainingEndgameCost = 4;  // в эндшпиле сильнее бережем карту, у которой не видно старших в масти
        private const int EndgameHardAttackBonus = 8;       // в эндшпиле уменьшает цену карты, которую сопернику трудно побить
        private const int LowCardThrowAwayBonus = 8;        // уменьшает цену мелкой карты при сбросе на соперника, который берет
        private const int HardCardThrowAwayPenalty = 12;    // увеличивает цену труднобьющейся карты, чтобы не скинуть ее как мусор
        private const int EarlyPressureBonus = 4;           // до эндшпиля немного поощряет подкидывание труднобьющейся карты
        private const int EndgamePressureBonus = 12;        // в эндшпиле сильно поощряет подкидывание труднобьющейся карты
        private const int HighestSuitPressureBonus = 6;     // уменьшает цену карты, если неизвестных старших карт этой масти нет
        private const int TrumpDefenseReservePenalty = 12;  // добавляется к цене козырной защиты, если неизвестных козырей еще много
        private const int EarlySafeCoverPenalty = 3;        // до эндшпиля слегка бережем защитную карту, которую трудно перебить
        private const int EndgameSafeCoverPenalty = 8;      // в эндшпиле сильнее бережем защитную карту, которую трудно перебить
        private const int UnknownHigherTrumpLimit = 1;      // козырь считаем труднобьющимся, если неизвестных старших козырей не больше
        private const int UnknownTrumpReserveLimit = 2;     // если неизвестных козырей не больше, некозырная сильная карта почти безопасна

      

        // Возвращает имя игрока
        public string GetName()
        {
            return name;
        }

        // количество карт на руке
        public int GetCount()
        {
            return hand.Count;
        }
        // Добавляет новую карту в руку
        public void AddToHand(SCard card)
        {
            hand.Add(card);
            Remember(card);
        }

        // Сделать ход (первый)
        public List<SCard> LayCards()
        {
            List<SCard> result = new List<SCard>();

            if (hand.Count == 0)
                return result;

            SCard bestLayCard = ChooseBestLayCard();
            result.Add(bestLayCard);
            hand.Remove(bestLayCard);

            return result;
        }

        // Отбиться.
        // На вход подается набор карт на столе, часть из них могут быть уже покрыты
        public bool Defend(List<SCardPair> table)
        {
            RememberTable(table);

            List<(int tableIndex, SCard coverCard)> plan = BuildDefensePlan(table);

            if (plan.Count != CountOpenCards(table))
                return false;

            return ApplyDefensePlan(table, plan);
        }

        // Подбросить карты
        // На вход подаются карты на столе
        public bool AddCards(List<SCardPair> table)
        {
            RememberTable(table);

            if (table.Count >= MTable.TotalCards)
                return false;

            List<int> ranksOnTable = GetRanksOnTable(table);
            List<SCard> candidates = GetAddCandidates(ranksOnTable);

            if (candidates.Count == 0)
                return false;

            bool defenderFailed = CountOpenCards(table) > 0;
            int freePlaces = MTable.TotalCards - table.Count;
            List<SCard> cardsToAdd = ChooseCardsToAdd(candidates, defenderFailed, freePlaces);

            if (cardsToAdd.Count == 0)
                return false;

            AddCardsToTable(table, cardsToAdd);

            return true;
        }

        // Вывести в консоль карты на руке
        public void ShowHand()
        {
            Console.WriteLine("Hand " + name);

            foreach (SCard card in hand)
            {
                MTable.ShowCard(card);
                Console.Write(MTable.Separator);
            }

            Console.WriteLine();
        }

        // Первый ход
        private SCard ChooseBestLayCard()
        {
            SCard bestCard = hand[0];

            foreach (SCard card in hand)
            {
                if (GetAttackCardCost(card) < GetAttackCardCost(bestCard))
                    bestCard = card;
            }

            return bestCard;
        }

        private bool ApplyDefensePlan(List<SCardPair> table, List<(int tableIndex, SCard coverCard)> plan)
        {
            foreach (var move in plan)
            {
                SCardPair pair = table[move.tableIndex];

                if (!pair.SetUp(move.coverCard, GetTrumpSuit()))
                    return false;

                table[move.tableIndex] = pair;
                hand.Remove(move.coverCard);
            }

            return true;
        }

        // Подкидывание
        private List<int> GetRanksOnTable(List<SCardPair> table)
        {
            List<int> ranks = new List<int>();

            foreach (SCardPair pair in table)
            {
                ranks.Add(pair.Down.Rank);

                if (pair.Beaten)
                    ranks.Add(pair.Up.Rank);
            }

            return ranks;
        }

        private List<SCard> GetAddCandidates(List<int> ranksOnTable)
        {
            List<SCard> candidates = new List<SCard>();

            foreach (SCard card in hand)
            {
                if (ranksOnTable.Contains(card.Rank))
                    candidates.Add(card);
            }

            return candidates;
        }

        private List<SCard> ChooseCardsToAdd(List<SCard> candidates, bool defenderFailed, int freePlaces)
        {
            if (defenderFailed)
                return ChooseCardsAfterFailedDefense(candidates, freePlaces);

            return ChooseCardsAfterSuccessfulDefense(candidates);
        }

        private List<SCard> ChooseCardsAfterFailedDefense(List<SCard> candidates, int freePlaces)
        {
            List<SCard> cardsToAdd = new List<SCard>();
            candidates.Sort((a, b) => GetThrowAwayCost(a).CompareTo(GetThrowAwayCost(b)));

            foreach (SCard card in candidates)
            {
                if (cardsToAdd.Count >= freePlaces)
                    break;

                if (IsTrump(card) && hand.Count > AggressiveTrumpHandLimit)
                    continue;

                cardsToAdd.Add(card);
            }

            return cardsToAdd;
        }

        private List<SCard> ChooseCardsAfterSuccessfulDefense(List<SCard> candidates)
        {
            List<SCard> cardsToAdd = new List<SCard>();
            candidates.Sort((a, b) => GetPressureCardCost(a).CompareTo(GetPressureCardCost(b)));

            foreach (SCard card in candidates)
            {
                if (IsTrump(card))
                    continue;

                if (!IsEndgame() && card.Rank >= HighRank && CountCardsWithRank(card.Rank) == 1)
                    continue;

                cardsToAdd.Add(card);
                break;
            }

            if (cardsToAdd.Count == 0 && IsEndgame())
                cardsToAdd.Add(candidates[0]);

            return cardsToAdd;
        }

        private void AddCardsToTable(List<SCardPair> table, List<SCard> cardsToAdd)
        {
            foreach (SCard card in cardsToAdd)
            {
                table.Add(new SCardPair(card));
                hand.Remove(card);
            }
        }

        // Общие проверки карт
        private bool IsTrump(SCard card)
        {
            return card.Suit == GetTrumpSuit();
        }

        private Suits GetTrumpSuit()
        {
            return MTable.GetTrump().Suit;
        }

        // Оценка стоимости карт
        private int GetCardCost(SCard card)
        {
            int cost = card.Rank;

            if (IsTrump(card))
                cost += IsEndgame() ? EndgameTrumpCost : EarlyTrumpCost;

            if (!IsTrump(card) && card.Rank >= HighRank)
                cost += HighCardCost;

            if (CountCardsWithRank(card.Rank) > 1)
                cost -= PairRankDiscount;

            if (!IsTrump(card) && IsHighestRemaining(card))
                cost += IsEndgame() ? HighestRemainingEndgameCost : HighestRemainingEarlyCost;

            return cost;
        }

        private int GetAttackCardCost(SCard card)
        {
            int cost = GetCardCost(card);

            if (IsEndgame() && IsHardToBeat(card))
                cost -= EndgameHardAttackBonus;

            return cost;
        }

        private int GetThrowAwayCost(SCard card)
        {
            int cost = GetCardCost(card);

            if (!IsTrump(card) && card.Rank <= LowRank)
                cost -= LowCardThrowAwayBonus;

            if (IsHardToBeat(card))
                cost += HardCardThrowAwayPenalty;

            return cost;
        }

        private int GetPressureCardCost(SCard card)
        {
            int cost = GetCardCost(card);

            if (IsHardToBeat(card))
                cost -= IsEndgame() ? EndgamePressureBonus : EarlyPressureBonus;

            if (!IsTrump(card) && CountUnknownHigherSameSuit(card) == 0)
                cost -= HighestSuitPressureBonus;

            return cost;
        }

        private bool CanBeat(SCard attack, SCard cover)
        {
            if (attack.Suit == cover.Suit)
            {
                return cover.Rank > attack.Rank;
            }

            if (IsTrump(cover) && !IsTrump(attack))
                return true;

            return false;
        }

        // Защита
        private List<(int tableIndex, SCard coverCard)> BuildDefensePlan(List<SCardPair> table)
        {
            List<int> openIndexes = GetOpenCardIndexes(table);
            List<SCard> availableCards = new List<SCard>(hand);
            List<(int tableIndex, SCard coverCard)> currentPlan = new List<(int, SCard)>();
            List<(int tableIndex, SCard coverCard)> bestPlan = new List<(int, SCard)>();
            int bestCost = int.MaxValue;

            void Search(int position, int currentCost)
            {
                if (currentCost >= bestCost)
                    return;

                if (position == openIndexes.Count)
                {
                    bestCost = currentCost;
                    bestPlan = new List<(int, SCard)>(currentPlan);
                    return;
                }

                int tableIndex = openIndexes[position];
                SCard attackCard = table[tableIndex].Down;
                List<SCard> candidates = GetCoverCandidates(attackCard, availableCards);

                foreach (SCard card in candidates)
                {
                    availableCards.Remove(card);
                    currentPlan.Add((tableIndex, card));

                    Search(position + 1, currentCost + GetDefenseCardCost(card, attackCard));

                    currentPlan.RemoveAt(currentPlan.Count - 1);
                    availableCards.Add(card);
                }
            }

            Search(0, 0);
            return bestPlan;
        }

        private List<int> GetOpenCardIndexes(List<SCardPair> table)
        {
            List<int> openIndexes = new List<int>();

            for (int i = 0; i < table.Count; i++)
            {
                if (!table[i].Beaten)
                    openIndexes.Add(i);
            }

            openIndexes.Sort((a, b) => GetAttackDifficulty(table[b].Down).CompareTo(GetAttackDifficulty(table[a].Down)));
            return openIndexes;
        }

        private List<SCard> GetCoverCandidates(SCard attackCard, List<SCard> availableCards)
        {
            List<SCard> candidates = new List<SCard>();

            foreach (SCard card in availableCards)
            {
                if (CanBeat(attackCard, card))
                    candidates.Add(card);
            }

            candidates.Sort((a, b) => GetDefenseCardCost(a, attackCard).CompareTo(GetDefenseCardCost(b, attackCard)));
            return candidates;
        }

        private int CountOpenCards(List<SCardPair> table)
        {
            int count = 0;

            foreach (SCardPair pair in table)
            {
                if (!pair.Beaten)
                    count++;
            }

            return count;
        }

        private int GetAttackDifficulty(SCard card)
        {
            int difficulty = card.Rank;

            if (IsTrump(card))
                difficulty += EarlyTrumpCost;

            return difficulty;
        }

        private int CountCardsWithRank(int rank)
        {
            int count = 0;

            foreach (SCard card in hand)
            {
                if (card.Rank == rank)
                    count++;
            }

            return count;
        }

        private bool IsEndgame()
        {
            return hand.Count <= EndgameHandLimit || CountUnknownCards() <= EndgameUnknownLimit;
        }

        private int GetDefenseCardCost(SCard cover, SCard attack)
        {
            int cost = GetCardCost(cover);

            if (IsTrump(cover) && !IsTrump(attack))
                cost += CountUnknownTrumpCards() > UnknownTrumpReserveLimit ? TrumpDefenseReservePenalty : 0;

            if (!IsTrump(cover) && CountUnknownHigherSameSuit(cover) == 0)
                cost += IsEndgame() ? EndgameSafeCoverPenalty : EarlySafeCoverPenalty;

            return cost;
        }

        // Подсчет неизвестных карт
        private int CountUnknownCards()
        {
            return DeckSize - seenCards.Count;
        }

        private int CountUnknownTrumpCards()
        {
            int count = 0;

            foreach (SCard card in GetUnknownCards())
            {
                if (IsTrump(card))
                    count++;
            }

            return count;
        }

        private int CountUnknownHigherSameSuit(SCard card)
        {
            int count = 0;

            foreach (SCard unknown in GetUnknownCards())
            {
                if (unknown.Suit == card.Suit && unknown.Rank > card.Rank)
                    count++;
            }

            return count;
        }

        private bool IsHighestRemaining(SCard card)
        {
            return CountUnknownHigherSameSuit(card) == 0;
        }

        private bool IsHardToBeat(SCard card)
        {
            if (IsTrump(card))
                return CountUnknownHigherSameSuit(card) <= UnknownHigherTrumpLimit;

            return CountUnknownHigherSameSuit(card) == 0 && CountUnknownTrumpCards() <= UnknownTrumpReserveLimit;
        }

        private List<SCard> GetUnknownCards()
        {
            List<SCard> cards = new List<SCard>();

            for (int suit = MinSuit; suit <= MaxSuit; suit++)
            {
                for (int rank = MinRank; rank <= MaxRank; rank++)
                {
                    SCard card = new SCard((Suits)suit, rank);

                    if (!seenCards.Contains(CardKey(card)))
                        cards.Add(card);
                }
            }

            return cards;
        }

        // Память увиденных карт
        private void RememberTable(List<SCardPair> table)
        {
            foreach (SCardPair pair in table)
            {
                Remember(pair.Down);

                if (pair.Beaten)
                    Remember(pair.Up);
            }
        }

        private void Remember(SCard card)
        {
            if (card.Rank < MinRank || card.Rank > MaxRank)
                return;

            seenCards.Add(CardKey(card));
        }

        private string CardKey(SCard card)
        {
            return ((int)card.Suit).ToString() + ":" + card.Rank.ToString();
        }
    }
}

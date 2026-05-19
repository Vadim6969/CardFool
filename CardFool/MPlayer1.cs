using System;
using System.Collections.Generic;

namespace CardFool
{
    internal class MPlayer1
    {
        private string name = "Vasya";
        private List<SCard> hand = new List<SCard>();       // карты на руке
        private HashSet<string> seenCards = new HashSet<string>();
        private const int DeckSize = 36;                    // размер колоды
        private const int MinSuit = 0;                      // минимальный номер масти в enum Suits
        private const int MaxSuit = 3;                      // максимальный номер масти в enum Suits
        private const int MinRank = 6;                      // младшая карта в колоде
        private const int MaxRank = 14;                     // старшая карта в колоде, туз
        private const int LowRank = 11;                     // валет и ниже считаются дешевыми картами
        private const int HighRank = 12;                    // дама и выше считаются старшими картами
        private const int EndgameHandLimit = 3;             // при таком числе карт на руке играем как в эндшпиле
        private const int EndgameUnknownLimit = 8;          // если неизвестных карт мало, включаем эндшпильную оценку
        private const int EarlyTrumpCost = 100;             // высокая цена козыря в начале и середине игры
        private const int EndgameTrumpCost = 35;            // сниженная цена козыря в конце игры
        private const int AggressiveTrumpHandLimit = 2;     // козыри можно подкидывать агрессивно только при малой руке
        private const int PairRankDiscount = 2;             // скидка карте, если есть еще карта такого же ранга
        private const int HighCardCost = 4;                 // штраф за старшую некозырную карту, ее полезно сохранить
        private const int HighestRemainingEarlyCost = 2;    // небольшой штраф за карту, старше которой не видно в масти
        private const int HighestRemainingEndgameCost = 4;  // в эндшпиле такую карту сохраняем сильнее
        private const int EndgameHardAttackBonus = 8;       // бонус к атаке картой, которую трудно побить в эндшпиле
        private const int LowCardThrowAwayBonus = 8;        // бонус к сбросу мелкой карты, когда соперник уже берет
        private const int HardCardThrowAwayPenalty = 12;    // не сбрасываем зря карту, которую трудно побить
        private const int EarlyPressureBonus = 4;           // умеренное давление сильной картой до эндшпиля
        private const int EndgamePressureBonus = 12;        // сильное давление труднобьющейся картой в эндшпиле
        private const int HighestSuitPressureBonus = 6;     // бонус карте, если неизвестных старших в этой масти нет
        private const int TrumpDefenseReservePenalty = 12;  // штраф за трату козыря на защиту, пока козырей еще много
        private const int EarlySafeCoverPenalty = 3;        // небольшой штраф за расход надежной карты в начале игры
        private const int EndgameSafeCoverPenalty = 8;      // в эндшпиле надежные карты бережем сильнее
        private const int UnknownTrumpReserveLimit = 2;     // порог, при котором неизвестных козырей уже мало

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

            if (GetCount() == 0) return result;

            SCard bestLayCard = hand[0];

            foreach (SCard card in hand)
            {
                if (GetAttackCardCost(card) < GetAttackCardCost(bestLayCard))
                {
                    bestLayCard = card;
                }
            }

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

            if (plan.Count < CountOpenCards(table))
                return false;

            foreach (var move in plan)
            {
                SCardPair pair = table[move.tableIndex];

                if (!pair.SetUp(move.coverCard, MTable.GetTrump().Suit))
                    return false;

                table[move.tableIndex] = pair;
                hand.Remove(move.coverCard);
            }

            return true;
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

            bool defenderFailed = HasOpenCards(table);
            int freePlaces = MTable.TotalCards - table.Count;
            List<SCard> cardsToAdd = ChooseCardsToAdd(candidates, defenderFailed, freePlaces);

            if (cardsToAdd.Count == 0)
                return false;

            AddCardsToTable(table, cardsToAdd);

            return true;
        }

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

        private bool HasOpenCards(List<SCardPair> table)
        {
            return CountOpenCards(table) > 0;
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


        // вспомогательные методы

        private bool IsTrump(SCard card)
        {
            return card.Suit == MTable.GetTrump().Suit;
        }

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

           if (IsTrump(cover) && !IsTrump(attack)) return true;

            return false;
        }

        private List<(int tableIndex, SCard coverCard)> BuildDefensePlan(List<SCardPair> table)
        {
            List<int> openIndexes = new List<int>();

            for (int i = 0; i < table.Count; i++)
            {
                if (!table[i].Beaten)
                    openIndexes.Add(i);
            }

            openIndexes.Sort((a, b) => GetAttackDifficulty(table[b].Down).CompareTo(GetAttackDifficulty(table[a].Down)));

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
                List<SCard> candidates = new List<SCard>();

                foreach (SCard card in availableCards)
                {
                    if (CanBeat(attackCard, card))
                        candidates.Add(card);
                }

                candidates.Sort((a, b) => GetDefenseCardCost(a, attackCard).CompareTo(GetDefenseCardCost(b, attackCard)));

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
                difficulty += 100;

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
                return CountUnknownHigherSameSuit(card) <= 1;

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

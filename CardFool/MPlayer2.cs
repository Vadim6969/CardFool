using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardFool
{
    internal class MPlayer2
    {
        private string name = "Masha";
        private List<SCard> hand = new List<SCard>();       // карты на руке

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
        }

        // Сделать ход (первый)
        public List<SCard> LayCards()
        {
            List<SCard> result = new List<SCard>();

            if (GetCount() == 0) return result;

            SCard bestLayCard = hand[0];

            foreach (SCard card in hand)
            {
                if (GetCardCost(card) < GetCardCost(bestLayCard))
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
            List<(int tableIndex, SCard coverCard)> plan = new List<(int, SCard)>();
            List<SCard> availableCards = new List<SCard>(hand);

            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].Beaten)
                    continue;

                SCard attackCard = table[i].Down;

                SCard bestCoverCard = new SCard();
                bool found = false;

                foreach (SCard card in availableCards)
                {
                    if (CanBeat(attackCard, card))
                    {
                        if (!found || GetCardCost(card) < GetCardCost(bestCoverCard))
                        {
                            bestCoverCard = card;
                            found = true;
                        }
                    }
                }

                if (!found)
                    return false;

                plan.Add((i, bestCoverCard));
                availableCards.Remove(bestCoverCard);
            }

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
            if (table.Count >= MTable.TotalCards)
                return false;

            List<int> ranksOnTable = new List<int>();

            foreach (SCardPair pair in table)
            {
                ranksOnTable.Add(pair.Down.Rank);

                if (pair.Beaten)
                    ranksOnTable.Add(pair.Up.Rank);
            }

            SCard bestAddCard = new SCard();
            bool found = false;

            foreach (SCard card in hand)
            {
                if (!ranksOnTable.Contains(card.Rank))
                    continue;

                if (!found || GetCardCost(card) < GetCardCost(bestAddCard))
                {
                    bestAddCard = card;
                    found = true;
                }
            }

            if (!found)
                return false;

            table.Add(new SCardPair(bestAddCard));
            hand.Remove(bestAddCard);

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


        // вспомогательные методы

        private bool IsTrump(SCard card)
        {
            return card.Suit == MTable.GetTrump().Suit;
        }

        private int GetCardCost(SCard card)
        {
            int cost = 100;

            if (IsTrump(card)) return cost + card.Rank;

            return card.Rank;
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
    }
}

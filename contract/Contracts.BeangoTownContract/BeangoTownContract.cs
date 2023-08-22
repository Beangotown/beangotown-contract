using System;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.BeangoTownContract
{
    /// <summary>
    /// The C# implementation of the contract defined in beango_town_contract.proto that is located in the "protobuf"
    /// </summary>
    public class BeangoTownContract : BeangoTownContractContainer.BeangoTownContractBase
    {
        public override Empty Initialize(Empty input)
        {
            if (State.Initialized.Value)
            {
                return new Empty();
            }

            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.ConsensusContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ConsensusContractSystemName);
            State.Admin.Value = Context.Sender;
            State.GameLimitSettings.Value = new GameLimitSettings()
            {
                DailyMaxPlayCount = BeangoTownContractConstants.DailyMaxPlayCount,
                DailyPlayCountResetHours = BeangoTownContractConstants.DailyPlayCountResetHours
            };
            State.GridTypeList.Value = new GridTypeList
            {
                Value =
                {
                    GridType.Blue, GridType.Blue, GridType.Red, GridType.Blue, GridType.Gold, GridType.Red,
                    GridType.Blue, GridType.Blue, GridType.Red, GridType.Gold, GridType.Blue, GridType.Red,
                    GridType.Blue, GridType.Blue, GridType.Gold, GridType.Red, GridType.Blue, GridType.Blue
                }
            };
            State.Initialized.Value = true;
            return new Empty();
        }


        private void InitPlayerInfo(bool resetStart)
        {
            var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = BeangoTownContractConstants.BeanPassSymbol,
                Owner = Context.Sender,
            });
            Assert(getBalanceOutput.Balance > 0, "BeanPass Balance is not enough");
            var playerInformation = GetCurrentPlayerInformation(Context.Sender, true);
            if (resetStart)
            {
                playerInformation.CurGridNum = 0;
            }

            Assert(playerInformation.PlayableCount > 0, "PlayableCount is not enough");
            playerInformation.PlayableCount--;
            State.PlayerInformation[Context.Sender] = playerInformation;
        }

        public override PlayOutput Play(PlayInput input)
        {
            InitPlayerInfo(input.ResetStart);
            var expectedBlockHeight = Context.CurrentHeight.Add(BeangoTownContractConstants.BingoBlockHeight);
            var boutInformation = new BoutInformation
            {
                PlayBlockHeight = Context.CurrentHeight,
                PlayId = Context.OriginTransactionId,
                PlayTime = Context.CurrentBlockTime,
                PlayerAddress = Context.Sender,
                ExpectedBlockHeight = expectedBlockHeight
            };
            State.BoutInformation[Context.OriginTransactionId] = boutInformation;
            Context.Fire(new Played()
            {
                PlayBlockHeight = boutInformation.PlayBlockHeight,
                PlayId = boutInformation.PlayId,
                PlayerAddress = boutInformation.PlayerAddress
            });
            return new PlayOutput { ExpectedBlockHeight = expectedBlockHeight };
        }

        public override Empty Bingo(Hash input)
        {
            Context.LogDebug(() => $"Getting game result of play id: {input.ToHex()}");

            CheckBingo(input, out var playerInformation, out var boutInformation, out var targetHeight);
            var randomHash = State.ConsensusContract.GetRandomHash.Call(new Int64Value
            {
                Value = targetHeight
            });

            Assert(randomHash != null && !randomHash.Value.IsNullOrEmpty(),
                "Still preparing your game result, please wait for a while :)");

            SetBoutInformationBingoInfo(input, randomHash, playerInformation, boutInformation);
            SetPlayerInformation(playerInformation, boutInformation);
            Context.Fire(new Bingoed
            {
                PlayBlockHeight = boutInformation.PlayBlockHeight,
                GridType = boutInformation.GridType,
                GridNum = boutInformation.GridNum,
                Score = boutInformation.Score,
                IsComplete = boutInformation.IsComplete,
                PlayId = boutInformation.PlayId,
                BingoBlockHeight = boutInformation.BingoBlockHeight,
                PlayerAddress = boutInformation.PlayerAddress
            });
            return new Empty();
        }

        private void SetPlayerInformation(PlayerInformation playerInformation, BoutInformation boutInformation)
        {
            playerInformation.CurGridNum = (playerInformation.CurGridNum + boutInformation.GridNum) %
                                           State.GridTypeList.Value.Value.Count;
            playerInformation.SumScore += boutInformation.Score;
            State.PlayerInformation[boutInformation.PlayerAddress] = playerInformation;
        }

        private void SetBoutInformationBingoInfo(Hash input, Hash randomHash, PlayerInformation playerInformation,
            BoutInformation boutInformation)
        {
            var randomNum = Convert.ToInt32(Math.Abs(randomHash.ToInt64() % 6) + 1);

            var gridType = State.GridTypeList.Value.Value[playerInformation.CurGridNum];
            boutInformation.Score = GetScoreByGridType(input, gridType, randomHash);
            boutInformation.IsComplete = true;
            boutInformation.GridNum = randomNum;
            boutInformation.GridType = gridType;
            boutInformation.BingoBlockHeight = Context.CurrentHeight;
            State.BoutInformation[input] = boutInformation;
        }

        private Int32 GetScoreByGridType(Hash input, GridType gridType, Hash randomHash)
        {
            int score;
            if (gridType == GridType.Blue)
            {
                score = BeangoTownContractConstants.BlueGridScore;
            }
            else if (gridType == GridType.Red)
            {
                score = BeangoTownContractConstants.RedGridScore;
            }
            else
            {
                var scoreHash = HashHelper.ConcatAndCompute(randomHash, input);
                score = Convert.ToInt32(Math.Abs(scoreHash.ToInt64() % 20) + 30);
            }

            return score;
        }

        private void CheckBingo(Hash input, out PlayerInformation playerInformation,
            out BoutInformation boutInformation, out long targetHeight)
        {
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid input.");

            playerInformation = State.PlayerInformation[Context.Sender];

            Assert(playerInformation != null, $"User {Context.Sender} not Login before.");

            boutInformation = State.BoutInformation[input];

            Assert(boutInformation != null, "Bout not found.");

            Assert(!boutInformation!.IsComplete, "Bout already finished.");

            targetHeight = boutInformation.PlayBlockHeight.Add(BeangoTownContractConstants.BingoBlockHeight);
            Assert(targetHeight <= Context.CurrentHeight, "Invalid target height.");
        }

        public override BoolValue CheckBeanPass(Address input)
        {
            var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = BeangoTownContractConstants.BeanPassSymbol,
                Owner = input
            });
            return new BoolValue { Value = getBalanceOutput.Balance > 0 };
        }

        public override Empty ChangeAdmin(Address input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");

            if (State.Admin.Value == input)
            {
                return new Empty();
            }

            State.Admin.Value = input;
            return new Empty();
        }

        public override Address GetAdmin(Empty input)
        {
            return State.Admin.Value;
        }

        public override BoutInformation GetBoutInformation(GetBoutInformationInput input)
        {
            Assert(input!.PlayId != null && !input.PlayId.Value.IsNullOrEmpty(), "Invalid playId.");

            var boutInformation = State.BoutInformation[input.PlayId];

            Assert(boutInformation != null, "Bout not found.");

            return boutInformation;
        }

        public override PlayerInformation GetPlayerInformation(Address input)
        {
            var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = BeangoTownContractConstants.BeanPassSymbol,
                Owner = input,
            });
            var nftEnough = getBalanceOutput.Balance > 0;

            var playerInformation = GetCurrentPlayerInformation(input, nftEnough);
            return playerInformation;
        }

        private PlayerInformation GetCurrentPlayerInformation(Address input, bool nftEnough)
        {
            var playerInformation = State.PlayerInformation[input];
            if (playerInformation == null)
            {
                playerInformation = new PlayerInformation
                {
                    PlayerAddress = input,
                    SumScore = 0,
                    CurGridNum = 0
                };
            }

            var gameLimitSettings = State.GameLimitSettings.Value;
            playerInformation.PlayableCount = GetPlayableCount(gameLimitSettings, playerInformation, nftEnough);
            return playerInformation;
        }

        private Int32 GetPlayableCount(GameLimitSettings gameLimitSettings, PlayerInformation playerInformation,
            bool nftEnough)
        {
            if (!nftEnough) return 0;
            var now = Context.CurrentBlockTime.ToDateTime();
            var playCountResetDateTime =
                new DateTime(now.Year, now.Month, now.Day, gameLimitSettings.DailyPlayCountResetHours, 0, 0,
                    DateTimeKind.Utc).ToTimestamp();
            if (playerInformation.LastPlayTime == null ||
                playerInformation.LastPlayTime.CompareTo(playCountResetDateTime) == -1)
            {
                return gameLimitSettings.DailyMaxPlayCount;
            }

            return playerInformation.PlayableCount;
        }

        public override GameLimitSettings GetGameLimitSettings(Empty input)
        {
            return State.GameLimitSettings.Value;
        }

        public override Empty SetGameLimitSettings(GameLimitSettings input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");
            Assert(input.DailyPlayCountResetHours is >= 0 and < 24, "Invalid input.");
            Assert(input.DailyMaxPlayCount >= 0, "Invalid input.");
            State.GameLimitSettings.Value = input;
            return new Empty();
        }
    }
}
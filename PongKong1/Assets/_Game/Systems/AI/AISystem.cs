using System.Collections.Generic;
using UnityEngine;
using PongKong.Systems.Ledge;

namespace PongKong.Systems.AI
{
    public enum AIDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    
    public enum PowerUpType { BallDash, ForceBounce, DestroyerBounce, BallVanish }

    public struct AIAction
    {
        public Vector2 MoveTarget;
    }

    public class AISystem : MonoBehaviour
    {
        [Header("Configuration")]
        public AIDifficulty difficulty = AIDifficulty.Medium;
        public PlayerSide aiSide = PlayerSide.Player2; 
        public float arenaTopY = 10f;
        public float arenaBottomY = -10f;
        public float aiLedgeX = 8.5f; 

        public AILedgeController aiLedgeController;

        // Event Channels
        public Vector2GameEvent onBallPositionUpdated;
        public PlayerSideGameEvent onBallEnteredCourt;
        public AIActionGameEvent onAIDecision;
        public PowerUpPrimedGameEvent onPowerUpPrimed;

        [Header("Ball State")]
        [SerializeField] private Vector2 currentBallPosition;
        [SerializeField] private Vector2 calculatedBallVelocity;
        [SerializeField] private Vector2 predictedInterceptPoint;
        [SerializeField] private bool isBallInAICourt;

        
        private Vector2 lastBallPosition;
        private float lastBallUpdateTime;
        private float lastReactionTime;

        
        private float reactionDelay;
        private float predictionAccuracy;
        private float errorMargin;

        private void Start()
        {
            ApplyDifficultyParameters();
            
            // Subscribe to EventBus here:
            if (onBallPositionUpdated != null) onBallPositionUpdated.OnRaised += HandleBallPositionUpdated;
            if (onBallEnteredCourt != null) onBallEnteredCourt.OnRaised += HandleBallEnteredCourt;
        }

        private void OnDestroy()
        {
            // Unsubscribe from EventBus here:
            if (onBallPositionUpdated != null) onBallPositionUpdated.OnRaised -= HandleBallPositionUpdated;
            if (onBallEnteredCourt != null) onBallEnteredCourt.OnRaised -= HandleBallEnteredCourt;
        }

        private void ApplyDifficultyParameters()
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    reactionDelay = 0.400f;
                    predictionAccuracy = 0.60f;
                    errorMargin = 3.0f;
                    break;
                case AIDifficulty.Medium:
                    reactionDelay = 0.200f;
                    predictionAccuracy = 0.80f;
                    errorMargin = 1.5f;
                    break;
                case AIDifficulty.Hard:
                    reactionDelay = 0.080f;
                    predictionAccuracy = 0.95f;
                    errorMargin = 0.5f;
                    break;
            }
        }

        
        public void HandleBallPositionUpdated(Vector2 position)
        {
            currentBallPosition = position;

            float dt = Time.time - lastBallUpdateTime;
            if (dt > 0f)
            {
                calculatedBallVelocity = (position - lastBallPosition) / dt;
            }
            
            lastBallPosition = position;
            lastBallUpdateTime = Time.time;

            if (Time.time - lastReactionTime >= reactionDelay)
            {
                lastReactionTime = Time.time;

                
                bool isMovingTowardsAI = (aiSide == PlayerSide.Player2 && calculatedBallVelocity.x > 0) || (aiSide == PlayerSide.Player1 && calculatedBallVelocity.x < 0);

                if (isMovingTowardsAI)
                {
                    predictedInterceptPoint = PredictBallIntercept();
                    ApplyIntentionalError();
                }
                else
                {
                    predictedInterceptPoint = new Vector2(aiLedgeX, 0f);
                }

                EmitAIDecision();
            }
        }
        public void HandleBallEnteredCourt(PlayerSide side)
        {
            if (side == aiSide)
            {
                isBallInAICourt = true;
                EvaluatePowerUpUsage();
            }
            else
            {
                isBallInAICourt = false;
            }
        }

        
        private Vector2 PredictBallIntercept()
        {
            if (Mathf.Abs(calculatedBallVelocity.x) < 0.001f)
            {
                return new Vector2(aiLedgeX, currentBallPosition.y);
            }

         
            float timeToIntercept = Mathf.Abs((aiLedgeX - currentBallPosition.x) / calculatedBallVelocity.x);
            float predictedY = currentBallPosition.y + (calculatedBallVelocity.y * timeToIntercept);
            float arenaHeight = arenaTopY - arenaBottomY;
            
            
            while (predictedY > arenaTopY || predictedY < arenaBottomY)
            {
                if (predictedY > arenaTopY)
                {
                    predictedY -= arenaHeight;
                }
                else if (predictedY < arenaBottomY)
                {
                    predictedY += arenaHeight;
                }
            }

            return new Vector2(aiLedgeX, predictedY);
        }

        private void ApplyIntentionalError()
        {
            if (Random.value > predictionAccuracy)
            {
                float randomOffset = Random.Range(-errorMargin, errorMargin);
                predictedInterceptPoint.y += randomOffset;

                predictedInterceptPoint.y = Mathf.Clamp(predictedInterceptPoint.y, arenaBottomY, arenaTopY);
            }
        }

        private void EmitAIDecision()
        {
            AIAction action = new AIAction { MoveTarget = predictedInterceptPoint };

            // Emit to EventBus:
            if (onAIDecision != null) onAIDecision.Raise(action);

            // Quick direct reference for testing (instead of EventBus)
            if (aiLedgeController != null) 
            {
                aiLedgeController.HandleAIDecision(action);
            }
        }

        private int GetAIPoints() { 
            return 5;
        } 
        private int GetAIScore() { 
            return 10;
        } 
        private int GetOpponentScore() { 
            return 10;
        
        } 
        private int GetAILedgeLives() { 
            return 3; } 
        private float GetBallSpeed() { 
            return calculatedBallVelocity.magnitude; }

        private void EvaluatePowerUpUsage()
        {
            int spendablePoints = GetAIPoints();
            if (spendablePoints == 0) return;

            bool usesPowerUp = false;

            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    usesPowerUp = Random.value < 0.1f;
                    break;
                case AIDifficulty.Medium:
                    usesPowerUp = Random.value < 0.4f; 
                    break;
                case AIDifficulty.Hard:
                    usesPowerUp = Random.value < 0.8f;
                    break;
            }

            if (usesPowerUp)
            {
                MakeStrategicPowerUpDecision(spendablePoints);
            }
        }

        private void MakeStrategicPowerUpDecision(int points)
        {
            bool isLedgeDamaged = GetAILedgeLives() < 3;
            bool isTrailing = GetOpponentScore() > GetAIScore();
            float speed = GetBallSpeed();

            PowerUpType chosenPowerUp = PowerUpType.BallDash; 
            bool triggerCombo = false;

            if (difficulty == AIDifficulty.Easy)
            {
                List<PowerUpType> affordable = GetAffordablePowerUps(points);
                if (affordable.Count > 0)
                {
                    chosenPowerUp = affordable[Random.Range(0, affordable.Count)];
                    PrimePowerUp(chosenPowerUp);
                }
                return;
            }

            if (isLedgeDamaged && points >= 1)
            {
                chosenPowerUp = PowerUpType.BallDash;
            }
            else if (difficulty == AIDifficulty.Hard && isTrailing && points >= 2)
            {
                chosenPowerUp = PowerUpType.DestroyerBounce;
            }
            else if (difficulty == AIDifficulty.Hard && speed >= 15f && points >= 4)
            {
                chosenPowerUp = PowerUpType.BallVanish;
            }
            else if (points >= 1)
            {
                chosenPowerUp = PowerUpType.ForceBounce;
            }



            if (difficulty == AIDifficulty.Hard && points >= 2)
            {
                if (chosenPowerUp == PowerUpType.ForceBounce || chosenPowerUp == PowerUpType.BallDash)
                {
                    triggerCombo = true;
                }
            }

            if (triggerCombo)
            {
                PrimePowerUp(PowerUpType.BallDash);
                PrimePowerUp(PowerUpType.ForceBounce);
            }
            else
            {
                PrimePowerUp(chosenPowerUp);
            }
        }

        private List<PowerUpType> GetAffordablePowerUps(int points)
        {
            List<PowerUpType> affordable = new List<PowerUpType>();
            if (points >= 1) affordable.Add(PowerUpType.BallDash);
            if (points >= 1) affordable.Add(PowerUpType.ForceBounce);
            if (points >= 2) affordable.Add(PowerUpType.DestroyerBounce);
            if (points >= 4) affordable.Add(PowerUpType.BallVanish);
            return affordable;
        }

        private void PrimePowerUp(PowerUpType powerUp)
        {
            // Simulate queueing the power-up to the EventBus
            if (onPowerUpPrimed != null) onPowerUpPrimed.Raise(powerUp, aiSide);
            Debug.Log($"[AISystem] Difficulty: {difficulty} - Priming PowerUp: {powerUp}");
        }
    }
}

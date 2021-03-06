﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace MyLib
{
    /// <summary>
    /// MoveAttackGoal
    /// 移动攻击
    /// </summary>
    public class CreepAttack : AttackState
    {
        private AINPC creepAI;
        private MoveController moveController;
        private EntityProxy target;
        private Vector2 initCenter;
        public override void EnterState()
        {
            base.EnterState();
            creepAI = aiCharacter.gameObject.GetComponent<AINPC>();
            moveController = aiCharacter.gameObject.GetComponent<MoveController>();
            target = aiCharacter.blackboard[AIParams.Target].entityProxy;
            initCenter = aiCharacter.aiNpc.mySelf.GetVec2Pos();
            aiCharacter.blackboard[AIParams.CenterPoint] = new AIEvent()
            {
                vec2 = initCenter,
            };
        }

        public override async Task RunLogic()
        {
            var otherAttr = target.actor.GetComponent<NpcAttribute>();
            var tempNum = runNum;
            while (CheckInState(tempNum) && !otherAttr.IsDead())
            {
                var mePos = creepAI.mySelf.GetVec2Pos();
                var tarPos = target.actor.GetVec2Pos();
                var dist = (mePos - tarPos).LengthSquared();

                var faraway = (mePos - initCenter).LengthSquared();
                var cfg = aiCharacter.aiNpc.npcConfig;
                var backDist = Math.Max(cfg.eyeSightDistance+0.2f, cfg.maxMoveRange2);
                if (faraway > backDist * backDist)
                {
                    aiCharacter.ChangeState(AIStateEnum.GO_BACK);
                }
                if (dist < creepAI.GetAttackRadiusSquare())
                {
                    await DoAttack();
                }
                else
                {
                    await DoMove();
                }
            }

            //敌人已经死亡
            if (CheckInState(tempNum))
            {
                aiCharacter.ChangeState(AIStateEnum.IDLE);
            }
        }

        /// <summary>
        /// 使用普通攻击技能 攻击目标 朝向
        /// </summary>
        /// <returns></returns>
        private async Task DoAttack()
        {
            var myself = creepAI.mySelf;
            var pos = creepAI.mySelf.GetIntPos();
            var skillAct = SkillAction.CreateBuilder();
            skillAct.Who = creepAI.mySelf.Id;
            skillAct.SkillId = creepAI.npcConfig.attackSkill;
            skillAct.SkillLevel = 0;
            skillAct.X = pos.x;
            skillAct.Y = pos.y;
            skillAct.Z = pos.z;

            var fp = target.actor.GetFloatPos();
            var myPos = creepAI.mySelf.GetFloatPos();
            var dir = fp - myPos;
            dir.Y = 0;
            //Unity 是顺时针为正向 左手坐标系
            myself.dir = ((int)MathUtil.Math2UnityRot(MathUtil.RotY(dir)));
            skillAct.Dir = myself.dir;

            skillAct.Target = target.actor.IDInRoom;

            var actConfig = creepAI.npcConfig.GetAction(ActionType.Attack);
            var tt = actConfig.totalTime;
            skillAct.RunFrame = Util.GameTimeToNet(tt);

            var gc = GCPlayerCmd.CreateBuilder();
            gc.Result = "Skill";
            gc.SkillAction = skillAct.Build();
            myself.GetRoom().AddNextFrameCmd(gc);

            var sk = aiCharacter.gameObject.GetComponent<SkillComponent>();
            var stateMachine = sk.CreateSkillStateMachine(skillAct.Build(), creepAI.npcConfig.normalAttack);
            await UpdateAction(stateMachine);
        }

        private async Task UpdateAction(SkillStateMachine stateMachine)
        {
            var actConfig = creepAI.npcConfig.GetAction(ActionType.Attack);
            var tempRunNum = runNum;

            await Task.Delay(Util.TimeToMS(actConfig.hitTime));
            //防止状态重入 导致的错误触发问题 一般在等待一段时间后执行
            if (CheckInState(tempRunNum))
            {
                stateMachine.OnEvent(SkillEvent.EventTrigger);
                await Task.Delay(Util.TimeToMS(actConfig.totalTime - actConfig.hitTime));
            }
        }


        /// <summary>
        /// 向目标靠近
        /// </summary>
        /// <returns></returns>
        private async Task DoMove()
        {
            var pos = target.actor.GetIntPos();
            //moveController.MoveTo(pos);
            var otherAttr = target.actor.GetComponent<NpcAttribute>();
            var tempNum = runNum;
            //检测和目标的距离
            while (CheckInState(tempNum) && !otherAttr.IsDead())
            {
                var tarNewPos = target.actor.GetIntPos();
                //寻路加移动 或者直线移动？
                moveController.MoveTo(tarNewPos);

                var mePos = creepAI.mySelf.GetVec2Pos();
                var tarPos = target.actor.GetVec2Pos();
                var dist = (mePos - tarPos).LengthSquared();
                //寻路追踪目标 需要时刻调整路径
                if(dist < creepAI.GetAttackRadiusSquare() * 0.9f)
                {
                    moveController.StopMove();
                    break;
                }

                //var waitTime = Util.FrameMSTime;
                //await Task.Delay(waitTime);
                await new WaitForNextFrame(creepAI.mySelf.GetRoom());
            }
            if (CheckInState(tempNum))
            {
                moveController.StopMove();
            }
        }

        /// <summary>
        /// 清理内部状态
        /// </summary>
        public override void ExitState()
        {
            moveController.StopMove();
            base.ExitState();
        }
    }
}

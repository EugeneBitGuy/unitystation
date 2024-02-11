using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using HealthV2;

namespace Systems.MobAIs
{
	[RequireComponent(typeof(MobMeleeAction))]
	[RequireComponent(typeof(ConeOfSight))]
	public class MimicAI : GenericHostileAI
	{
		[SerializeField] private float mimicryDistance;
		[SerializeField] private float criticalDistance;

		[SerializeField] private LayerMask mimickiableLayer;

		private bool _isMimicry;

		protected override void OnAIStart()
		{
			ResetBehaviours();
			_isMimicry = false;
		}

		public override void ContemplatePriority()
		{

			if (!isServer) return;

			if (IsDead || IsUnconscious)
			{
				HandleDeathOrUnconscious();
			}

			MimicLoop();
			MonitorFleeingTime();
		}

		private void MimicLoop()
		{
			switch(currentStatus)
			{
				case MobStatus.None:
					MonitorIdleness();
					break;
				case MobStatus.Searching:
					HandleSearch();
					break;
				case MobStatus.Attacking:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override void MonitorIdleness()
		{
			if (!_isMimicry)
				HandleTrueFormIdleness();
			else
				HandleMimicFormIdleness();
		}

		private void HandleTrueFormIdleness()
		{
			bool shouldMimicry = ArePlayersAtDistance(mimicryDistance);
			if(shouldMimicry)
			{
				Mimic();
			}
			else
			{
				DoRandomMove();
			}
		}

		private void HandleMimicFormIdleness()
		{
			bool arePlayersAtCriticalDistance = ArePlayersAtDistance(criticalDistance);
			bool shouldStillMimicry = ArePlayersAtDistance(mimicryDistance);

			if (!shouldStillMimicry)
			{
				StopMimic();
				return;
			}

			if (arePlayersAtCriticalDistance)
			{
				BeginSearch();
			}
		}

		protected override void HandleSearch()
		{
			moveWaitTime += MobController.UpdateTimeInterval;
			searchWaitTime += MobController.UpdateTimeInterval;
			if (!(searchWaitTime >= searchTickRate)) return;
			searchWaitTime = 0f;
			var findTarget = SearchForTarget();
			if (findTarget != null)
			{
				BeginAttack(findTarget);
			}
			else if(findTarget == null)
			{
				ResetBehaviours();
			}
		}

		private void Mimic()
		{
			if(_isMimicry) return;
			_isMimicry = true;

			mobFlee.fleeTarget = null;
			fleeingStopped.RemoveAllListeners();


			ResetBehaviours();
			objectPhysics.isNotPushable = true;
			Morph();
		}

		private void StopMimic()
		{
			if(!_isMimicry) return;
			_isMimicry = false;

			ResetBehaviours();
			objectPhysics.isNotPushable = false;
			Morph(true);
		}

		private void Morph(bool isMorphBack = false)
		{
			if (isMorphBack)
			{
				mobSprite.SetToAlive(true);
				Chat.AddActionMsgToChat(gameObject, $"{mobName} has morphed into default form");
				return;
			}

			var objectsCanBeMimicable = coneOfSight.GetObjectInFieldOfView(mimickiableLayer, LayerTypeSelection.None, 10);

			if (objectsCanBeMimicable.Count == 0) return;

			var objectToMimic = objectsCanBeMimicable.Where(obj => obj != null).PickRandom();
			mobSprite.SetNewSprite(objectToMimic.GetComponentInChildren<SpriteHandler>().GetCurrentSpriteSO());

			Chat.AddActionMsgToChat(gameObject, $"{mobName} has morphed into {objectToMimic.gameObject.name}");
		}

		protected override void BeginAttack(GameObject target)
		{
			StopMimic();
			currentStatus = MobStatus.Attacking;
			StartCoroutine(Stalk(target));
		}

		private IEnumerator Stalk(GameObject stalked)
		{
			while (ArePlayersAtDistance(mimicryDistance))
			{
				if(mobMeleeAction.FollowTarget == null)
				{
					mobMeleeAction.StartFollowing(stalked);
				}
				yield return WaitFor.Seconds(.2f);
			}
			ResetBehaviours();
			yield break;
		}

		protected override void OnAttackReceived(GameObject damagedBy = null)
		{
			if (damagedBy != null)
			{
				ResetBehaviours();
				StartFleeing(damagedBy, 2f);
				fleeingStopped.AddListener(Mimic);
			}
		}

		public override void LocalChatReceived(ChatEvent chatEvent)
		{
			if(currentStatus != MobStatus.Searching) return;

			base.LocalChatReceived(chatEvent);
		}

		private bool ArePlayersAtDistance(float distance)
		{
			var hits = coneOfSight.GetObjectInFieldOfView(hitMask, LayerTypeSelection.None,  distance);

			if (hits.Count == 0) return false;

			foreach (var coll in hits)
			{
				if (coll == null) continue;

				if (coll.layer == playersLayer
				    && !coll.GetComponent<LivingHealthMasterBase>().IsDead)
				{
					return true;
				}
			}

			return false;
		}
	}
}

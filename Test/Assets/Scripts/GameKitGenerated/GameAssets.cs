// GameKit 뼈대 생성물.
using LayonCraft.GameKit;
using UnityEngine;

[CreateAssetMenu(fileName = "PrefabAssetReference", menuName = "SO/PrefabAssetReference")]
public class PrefabAssetReference : AssetReferenceBase<PrefabData, ContainLabel, GameObject> { }

[CreateAssetMenu(fileName = "SoundAssetReference", menuName = "SO/SoundAssetReference")]
public class SoundAssetReference : AssetReferenceBase<SoundData, ContainLabel, AudioClip> { }

[CreateAssetMenu(fileName = "GameTextTable", menuName = "SO/GameTextTable")]
public class GameTextTable : GameTextTableBase<GameTextData> { }

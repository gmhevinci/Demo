﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MotionFramework.Resource;

namespace Hotfix
{
	public class UIManager
	{
		public static readonly UIManager Instance = new UIManager();

		private readonly Dictionary<EWindowType, Type> _types = new Dictionary<EWindowType, Type>();
		private readonly List<UIWindow> _stack = new List<UIWindow>(100);
		private AssetObject _assetRoot;
		private GameObject _uiRoot;
		private GameObject _uiDesktop;
		private Camera _uiCamera;


		private UIManager()
		{
		}
		public void Start()
		{
			// 收集所有窗口类型
			CollectTypes();

			// 创建UIRoot
			CreateUIRoot();
		}
		public void Update()
		{
			// 更新窗口
			int count = _stack.Count;
			for (int i = 0; i < _stack.Count; i++)
			{
				if (_stack.Count != count)
					break;
				UIWindow window = _stack[i];
				if (window.IsPrepare && window.IsOpen)
					window.InternalUpdate();
			}
		}
		public void Destroy()
		{
			DestroyUIRoot();

			// 销毁所有窗口
			for (int i = 0; i < _stack.Count; i++)
			{
				UIWindow window = _stack[i];
				window.InternalDestroy();
			}
			_stack.Clear();
		}

		/// <summary>
		/// 获取UI相机
		/// </summary>
		public Camera GetUICamera()
		{
			return _uiCamera;
		}

		/// <summary>
		/// 获取UI桌面对象
		/// </summary>
		public GameObject GetUIDesktop()
		{
			return _uiDesktop;
		}

		/// <summary>
		/// 检测UIRoot是否准备完毕
		/// </summary>
		public bool IsPrepareUIRoot()
		{
			if (_assetRoot == null)
				return false;
			return _assetRoot.Result == EAssetResult.OK;
		}

		/// <summary>
		/// 打开窗口
		/// </summary>
		/// <param name="type">窗口类型</param>
		/// <param name="userData">用户数据</param>
		public void OpenWindow(EWindowType type, System.Object userData = null)
		{
			UIWindow window;

			// 如果窗口已经存在
			if (IsContains(type))
			{
				window = GetWindow(type);
				Pop(window); //弹出旧窗口
			}
			else
			{
				// 创建窗口类
				window = CreateWindowClass(type);
			}

			Push(window);
			window.InternalOpen(userData);
			window.InternalLoad(OnWindowPrepare);
			HotfixLogger.Log($"Open window {type}");
		}

		/// <summary>
		/// 关闭窗口
		/// </summary>
		/// <param name="type">窗口类型</param>
		public void CloseWindow(EWindowType type)
		{
			UIWindow window = GetWindow(type);
			if (window != null)
			{
				if (window.DontDestroy)
				{
					window.InternalClose();
				}
				else
				{
					window.InternalDestroy();
					Pop(window);
					OnSortWindowDepth(window.WindowLayer);
					OnSetWindowVisible(window.WindowLayer);
				}
				HotfixLogger.Log($"Close window {type}");
			}
		}

		/// <summary>
		/// 关闭所有窗口（常驻窗口除外）
		/// </summary>
		public void CloseAll()
		{
			List<UIWindow> tempList = new List<UIWindow>();
			for (int i = 0; i < _stack.Count; i++)
			{
				UIWindow window = _stack[i];
				if (window.DontDestroy == false)
					tempList.Add(window);
			}

			for (int i = 0; i < tempList.Count; i++)
			{
				UIWindow window = tempList[i];
				CloseWindow(window.WindowType);
			}
		}

		// UIRoot相关
		private void CreateUIRoot()
		{
			if (_assetRoot == null)
			{
				_assetRoot = new AssetObject();
				_assetRoot.Load("UIPanel/UIRoot", OnUIRootLoad);
			}
		}
		private void DestroyUIRoot()
		{
			if (_assetRoot != null)
			{
				_assetRoot.UnLoad();
				_assetRoot = null;
			}

			if (_uiRoot != null)
			{
				GameObject.Destroy(_uiRoot);
				_uiRoot = null;
			}
		}
		private void OnUIRootLoad(Asset asset)
		{
			_uiRoot = _assetRoot.GetMainAsset<GameObject>();
			_uiDesktop = _uiRoot.transform.FindChildByName("UIDesktop").gameObject;
			_uiCamera = _uiRoot.transform.FindChildByName("UICamera").GetComponent<Camera>();
			GameObject.DontDestroyOnLoad(_uiRoot);
		}

		// 准备完毕
		private void OnWindowPrepare(UIWindow window)
		{
			window.OnRefresh();
			OnSortWindowDepth(window.WindowLayer);
			OnSetWindowVisible(window.WindowLayer);			
		}
		// 排序深度
		private void OnSortWindowDepth(EWindowLayer layer)
		{
			int depth = (int)layer;
			for (int i = 0; i < _stack.Count; i++)
			{
				if (_stack[i].WindowLayer == layer)
				{
					_stack[i].Depth = depth;
					depth += 100; //注意：每次递增100深度
				}
			}
		}
		// 刷新可见性
		private void OnSetWindowVisible(EWindowLayer layer)
		{
			bool isHideNext = false;
			for (int i = _stack.Count - 1; i >= 0; i--)
			{
				UIWindow window = _stack[i];
				if (window.WindowLayer == layer)
				{
					if (isHideNext == false)
					{
						window.IsVisible = true;
						if (window.IsPrepare && window.IsOpen && window.FullScreen)
							isHideNext = true;
					}
					else
					{
						window.IsVisible = false;
					}
				}
			}
		}

		private UIWindow GetWindow(EWindowType type)
		{
			for (int i = 0; i < _stack.Count; i++)
			{
				UIWindow window = _stack[i];
				if (window.WindowType == type)
					return window;
			}
			return null;
		}
		private bool IsTopWindow(EWindowType type, EWindowLayer layer)
		{
			UIWindow lastOne = null;
			for (int i = 0; i < _stack.Count; i++)
			{
				if (_stack[i].WindowLayer == layer)
					lastOne = _stack[i];
			}

			if (lastOne == null)
				return false;

			return lastOne.WindowType == type;
		}
		private bool IsContains(EWindowType type)
		{
			for (int i = 0; i < _stack.Count; i++)
			{
				UIWindow window = _stack[i];
				if (window.WindowType == type)
					return true;
			}
			return false;
		}
		private void Push(UIWindow window)
		{
			// 如果已经存在
			if (IsContains(window.WindowType))
				throw new System.Exception($"Window {window.WindowType} is exist.");

			// 获取插入到所属层级的位置
			int insertIndex = -1;
			for (int i = 0; i < _stack.Count; i++)
			{
				if (window.WindowLayer == _stack[i].WindowLayer)
					insertIndex = i + 1;
			}

			// 如果没有所属层级，找到相邻层级
			if (insertIndex == -1)
			{
				for (int i = 0; i < _stack.Count; i++)
				{
					if (window.WindowLayer > _stack[i].WindowLayer)
						insertIndex = i + 1;
				}
			}

			// 如果是空栈或没有找到插入位置
			if (insertIndex == -1)
			{
				insertIndex = 0;
			}

			// 最后插入到堆栈
			_stack.Insert(insertIndex, window);
		}
		private void Pop(UIWindow window)
		{
			// 从堆栈里移除
			_stack.Remove(window);
		}

		private void CollectTypes()
		{
			List<Type> types = ILRManager.Instance.HotfixAssemblyTypes;
			for (int i = 0; i < types.Count; i++)
			{
				System.Type type = types[i];

				// 判断属性标签
				if (Attribute.IsDefined(type, typeof(WindowAttribute)))
				{
					// 判断是否重复
					WindowAttribute attribute = HotfixTypeHelper.GetAttribute<WindowAttribute>(type);
					if (_types.ContainsKey(attribute.WindowType))
						throw new Exception($"Window type {attribute.WindowType} already exist.");

					// 添加到集合
					_types.Add(attribute.WindowType, type);
				}
			}
		}
		private UIWindow CreateWindowClass(EWindowType windowType)
		{
			UIWindow window = null;
			Type type;
			if (_types.TryGetValue(windowType, out type))
			{
				// 注意：ILR下通过CreateInstance传递构造函数的参数无效
				WindowAttribute attribute = HotfixTypeHelper.GetAttribute<WindowAttribute>(type);
				window = (UIWindow)Activator.CreateInstance(type);
				window.Init(attribute.WindowType, attribute.WindowLayer);
			}

			if (window == null)
				throw new KeyNotFoundException($"{nameof(UIWindow)} {windowType} is not define.");

			return window;
		}
	}
}
// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "MuR/Ptr.h"
#include "MuR/RefCounted.h"
#include "MuT/Node.h"
#include "MuT/NodeMesh.h"


namespace mu
{

	// Forward definitions
	class NodeScalar;
	typedef Ptr<NodeScalar> NodeScalarPtr;
	typedef Ptr<const NodeScalar> NodeScalarPtrConst;

	class NodeMeshGeometryOperation;
	typedef Ptr<NodeMeshGeometryOperation> NodeMeshGeometryOperationPtr;
	typedef Ptr<const NodeMeshGeometryOperation> NodeMeshGeometryOperationPtrConst;


	//! Node that morphs a base mesh with one or two weighted targets from a sequence.
	class MUTABLETOOLS_API NodeMeshGeometryOperation : public NodeMesh
	{
	public:

		NodeMeshGeometryOperation();

		//-----------------------------------------------------------------------------------------
		// Node Interface
		//-----------------------------------------------------------------------------------------

		const FNodeType* GetType() const override;
		static const FNodeType* GetStaticType();

		//-----------------------------------------------------------------------------------------
		// Own Interface
		//-----------------------------------------------------------------------------------------

		const NodeMeshPtr& GetMeshA() const;
		void SetMeshA( const NodeMeshPtr& );

		const NodeMeshPtr& GetMeshB() const;
		void SetMeshB(const NodeMeshPtr&);

		const NodeScalarPtr& GetScalarA() const;
		void SetScalarA(const NodeScalarPtr&);

		const NodeScalarPtr& GetScalarB() const;
		void SetScalarB(const NodeScalarPtr&);

        //-----------------------------------------------------------------------------------------
		// Interface pattern
		//-----------------------------------------------------------------------------------------
		class Private;
		Private* GetPrivate() const;

	protected:

		//! Forbidden. Manage with the Ptr<> template.
		~NodeMeshGeometryOperation();

	private:

		Private* m_pD;

	};

}
import React, { useEffect, useState, useCallback } from 'react';
import  * as BS from 'react-bootstrap'
import 'reactjs-popup/dist/index.css';

import ProfileCard from './ProfileCard.js'
import Loading from "./Loading.js";
import API_URL from "../Constants/constants.js";

export default function Memberlist(props) {
    
    const sysID = props.sysID;

    const [isLoading, setIsLoading ] = useState(false);
    const [isError, setIsError ] = useState(false);

    const [currentPage, setCurrentPage] = useState(1);
    const [membersPerPage, setMembersPerPage] = useState(25);

    const [members, setMembers ] = useState([]);
    const [value, setValue] = useState('');

  const fetchMembers = useCallback( () => {
    setIsLoading(true);
    setIsError(false);
    setMembersPerPage(25);

     fetch(`${API_URL}s/${sysID}/members`,{
    method: 'GET',
    }).then ( res => res.json()
    ).then (data => { 
    setMembers(data)
      setIsLoading(false);
  })
    .catch (error => { 
        console.log(error);
        setIsError(true);
        setIsLoading(false);
    })
  }, [sysID])

  useEffect(() => {
    fetchMembers();
  }, [fetchMembers])

    const indexOfLastMember = currentPage * membersPerPage;
    const indexOfFirstMember = indexOfLastMember - membersPerPage;
    const currentMembers =  members.filter(member => {
      if (!value) return true;
      if (member.name.toLowerCase().includes(value.toLowerCase())) {
        return true;
      }
      return false;
    }).sort((a, b) => a.name.localeCompare(b.name)).slice(indexOfFirstMember, indexOfLastMember);


    const active = currentPage;
    const pageAmount = Math.ceil(members.length / membersPerPage);
      
      const memberList = currentMembers.map((member) => <BS.Card key={member.id}>
        <ProfileCard
        member={member} 
        />
    </BS.Card>
    );

    return (
        <>
        { isLoading ? <Loading /> : isError ? 
        <BS.Alert variant="danger">Error fetching members. Perhaps the member list has been set to private.</BS.Alert> :
        <>
        <BS.Row noGutters="true" className="justify-content-md-center">
        <BS.Col className="lg-2 mb-3" xs={12} lg={3}>
        <BS.Form>
          <BS.InputGroup className="mb-3">
          <BS.Form.Control disabled placeholder='Page length:'/>
            <BS.Form.Control as="select" onChange={e => {
              setMembersPerPage(e.target.value);
              setCurrentPage(1);
              }}>
              <option>10</option>
              <option selected>25</option>
              <option>50</option>
              <option>100</option>
            </BS.Form.Control>
            </BS.InputGroup>
        </BS.Form>
        </BS.Col>
        <BS.Col className="ml-lg-2 mb-3" xs={12} lg={4}>
        <BS.Form>
            <BS.Form.Control value={value} onChange={e => {setValue(e.target.value); setCurrentPage(1)}} placeholder="Search"/>
        </BS.Form>
        </BS.Col>
        <BS.Col className="ml-lg-2 mb-3" xs={12} lg={1}>
          <BS.Button type="primary" className="m-0" block onClick={() => fetchMembers()}>Refresh</BS.Button>
        </BS.Col>
        </BS.Row>
        <BS.Row className="justify-content-md-center">
          <BS.Pagination className="ml-auto mr-auto">
          { currentPage === 1 ? <BS.Pagination.Prev disabled/> : <BS.Pagination.Prev onClick={() => setCurrentPage(currentPage - 1)} />}
          { currentPage < 3 ? "" : <BS.Pagination.Item  onClick={() => setCurrentPage(1)} active={1 === active}>{1}</BS.Pagination.Item>}
          { currentPage < 4 ? "" :<BS.Pagination.Ellipsis disabled />}
          { currentPage > 1 ? <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage - 1)}>{currentPage - 1}</BS.Pagination.Item> : "" }
          <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage)} active={currentPage === active}>{currentPage}</BS.Pagination.Item>
          { currentPage < pageAmount ? <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage + 1)}>{currentPage + 1}</BS.Pagination.Item> : "" }
          { currentPage > pageAmount - 3 ? "" : <BS.Pagination.Ellipsis disabled />}
          { currentPage > pageAmount - 2 ? "" : <BS.Pagination.Item  onClick={() => setCurrentPage(pageAmount)} active={pageAmount === active}>{pageAmount}</BS.Pagination.Item>}
          { currentPage === pageAmount ? <BS.Pagination.Next disabled /> :<BS.Pagination.Next onClick={() => setCurrentPage(currentPage + 1)} />}
          </BS.Pagination>
        </BS.Row>
        <BS.Accordion className="mb-3 mt-3 w-100" defaultActiveKey="0">
            {memberList}
        </BS.Accordion>
        </>
        }
        </>
    )
}
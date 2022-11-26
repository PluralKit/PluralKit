<script lang="ts">
    import { Container, Card, CardHeader, CardBody, Row, Col, CardTitle } from 'sveltestrap';
    import { Link } from 'svelte-navigator';
</script>

<Container>
    <Row>
        <Col class="mx-auto" xs={12} lg={11} xl={10}>
            <Card>
                <CardHeader>
                    <CardTitle style="margin-top: 8px; outline: none;">404. Page not found</CardTitle>
                </CardHeader>
                <CardBody>
                    Looks like this page doesn't exist. <Link to="/">Go home</Link>?
                </CardBody>
            </Card>
        </Col>
    </Row>
</Container>
